using System.Globalization;
using System.Text;
using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Hạng mục 7: dấu thời gian do bên thứ ba cấp có đóng lên <b>đúng bộ số đang được công bố</b> hay
/// không. Đây là chỗ duy nhất biến lập luận "cam kết có TRƯỚC khi block đích tồn tại" từ lời khẳng
/// định của ban tổ chức thành bằng chứng: khoá ký thuộc về nơi cấp dấu, không thuộc ban tổ chức.
///
/// Ba điều hạng mục này KHÔNG làm, và không được để người đọc tưởng là có:
///  1. Không thẩm định chữ ký và chuỗi chứng thư của token — việc đó cần token thô và <c>openssl</c>
///     (xem <c>backend/docs/operations/verify-timestamp-tokens.md</c>). Ở đây chỉ kiểm NỘI DUNG được
///     đóng dấu, nên một token bịa đặt vẫn lọt; giá trị của hạng mục nằm ở chỗ bắt được việc đem dấu
///     thật đóng lên một bộ số rồi trưng ra một bộ số khác.
///  2. Không so <c>genTime</c> với thời điểm block đích được đào — đó là hạng mục mốc neo (vé #15).
///  3. Không kết luận thay cho dấu của các mốc khác (<c>STEPCHAIN:*</c>, <c>RESULTS:*</c>): chúng
///     được đếm ra số liệu thô để người kiểm biết chúng tồn tại, nhưng không gộp vào kết luận này.
///
/// Một kết luận cho cả mốc cam kết chứ không phải mỗi dấu một kết luận, vì <b>chốt lại</b>
/// (<c>refreeze</c>) là thao tác hợp lệ trước khi mở cổng: sau khi chốt lại, dấu của lần chốt trước
/// đóng lên bộ số cũ và sẽ không khớp dữ liệu đang công bố. Kết luận theo từng dấu sẽ biến chuyện
/// đó thành báo động giả; đúng quy tắc là "còn một dấu khớp dữ liệu đang công bố thì mốc cam kết có
/// bằng chứng thời gian, và số dấu không khớp phải được nói ra để đối chiếu với biên bản".
/// </summary>
internal static class FreezeTimestampCheck
{
    private const string Ten = "Dấu thời gian mốc cam kết";

    private const string PhienBan = "NOXH-FREEZE-v1";

    private const string NhanKhongRoNoiCap = "(không rõ nơi cấp)";

    /// <summary>Đúng các dòng của <c>NOXH-FREEZE-v1</c> — thừa hay thiếu dòng đều là định dạng lạ.</summary>
    private static readonly string[] KhoaBatBuoc =
    [
        "projectId", "listHash", "rSup",
        "rServerCommitA", "rServerCommitB", "rServerCommitC",
        "ethTargetHeight", "btcTargetHeight", "anchorFrozenAt",
    ];

    private const string KhongCoDauGiaiThich =
        "Mốc cam kết KHÔNG có bằng chứng thời gian độc lập: báo cáo không công bố dấu thời gian nào đóng lên "
        + "bộ số đã chốt. Không có dấu thì việc \"cam kết có trước khi block neo tồn tại\" chỉ còn là lời của "
        + "ban tổ chức, không ai đối chiếu được — hãy đòi bản sao token và biên bản của buổi lễ trước khi tin phần "
        + "còn lại của báo cáo.";

    private const string DatGiaiThich =
        "Dấu thời gian do bên thứ ba cấp đóng đúng lên bộ số đang được công bố: định danh dự án, phần ngẫu nhiên "
        + "của tổ giám sát, cả ba cam kết ngẫu nhiên máy chủ, hai mốc block đích và thời điểm chốt đều trùng khít "
        + "với chuỗi nằm trong dấu. Nghĩa là bộ số này đã tồn tại từ lúc dấu được cấp, không phải bộ số dựng lại "
        + "sau khi đã biết kết quả.";

    public static IEnumerable<CheckResult> Run(VerificationInput input)
    {
        yield return Kiem(input.Report);
    }

    private static CheckResult Kiem(TransparencyReport bc)
    {
        var tatCa = DauThoiGianChung.TatCa(bc);
        var moc = DauThoiGianChung.MocCamKet(tatCa);

        var soLieu = new List<CheckMetric>
        {
            new("Số dấu thời gian của mốc cam kết", moc.Count.ToString()),
            new("Số dấu thời gian của mốc khác (không thuộc hạng mục này)", (tatCa.Count - moc.Count).ToString()),
        };

        if (moc.Count == 0)
            return new CheckResult(CheckIds.FreezeTimestamp, Ten, CheckStatus.KhongKiemDuoc, KhongCoDauGiaiThich)
            {
                Metrics = soLieu,
            };

        foreach (var t in moc)
        {
            soLieu.Add(new CheckMetric($"Thời điểm cấp dấu — {NoiCap(t)}", NguonNgauNhienChung.Co(t.GenTime)));
            soLieu.Add(new CheckMetric($"Số hiệu dấu — {NoiCap(t)}", NguonNgauNhienChung.Co(t.SerialNumber)));
        }

        CheckResult ChuaKiemDuoc(string vi, string? preimage = null) =>
            new(CheckIds.FreezeTimestamp, Ten, CheckStatus.KhongKiemDuoc, vi, Preimage: preimage)
            {
                Metrics = soLieu,
            };

        // Bước 1: băm lại chuỗi đóng dấu của TỪNG dấu. Chỉ cần một dấu tự mâu thuẫn với vân tay của
        // chính nó là bằng chứng đã hỏng — không được lấy dấu còn lại che cho nó.
        foreach (var t in moc)
        {
            if (string.IsNullOrWhiteSpace(t.Preimage))
                return ChuaKiemDuoc(
                    $"Dấu do {NoiCap(t)} cấp không công bố chuỗi đã đem đóng dấu, nên không băm lại được để biết "
                    + "dấu này đóng lên bộ số nào.");

            if (Hex.Doc(t.Digest) is null)
                return ChuaKiemDuoc(
                    string.IsNullOrWhiteSpace(t.Digest)
                        ? $"Dấu do {NoiCap(t)} cấp không công bố dấu vân tay, nên không có gì để đối chiếu với giá "
                          + "trị băm lại từ chuỗi đóng dấu."
                        : $"Dấu vân tay của dấu do {NoiCap(t)} cấp không phải chuỗi mã băm hợp lệ, nên không đối "
                          + "chiếu được với giá trị băm lại.",
                    t.Preimage);

            var tinhDuoc = Hex.Sha256Hex(Encoding.UTF8.GetBytes(t.Preimage));

            if (!string.Equals(tinhDuoc, Hex.ChuanHoa(t.Digest), StringComparison.Ordinal))
            {
                // Hai lỗi copy/dán làm lệch dấu vân tay dù nội dung không hề bị sửa (xuống dòng kiểu
                // Windows, mất dòng trống cuối). Gọi đó là dữ liệu bị sửa là vu oan cho báo cáo —
                // phải nói đúng bệnh: bản đang dùng không phải bản gốc từng byte.
                if (LoiSaoChep(t.Preimage, Hex.ChuanHoa(t.Digest)!))
                    return ChuaKiemDuoc(
                        $"Chuỗi đóng dấu của dấu do {NoiCap(t)} cấp khớp dấu vân tay chỉ khi sửa lại ký tự xuống "
                        + "dòng: bản báo cáo đang dùng đã qua chỉnh sửa hoặc copy/dán chứ không còn nguyên từng "
                        + "byte như lúc công bố. Hãy tải lại bản gốc rồi kiểm lại — chưa kết luận được gì từ bản này.",
                        t.Preimage);

                return new CheckResult(
                    CheckIds.FreezeTimestamp,
                    Ten,
                    CheckStatus.KhongDat,
                    $"Dấu vân tay trong dấu do {NoiCap(t)} cấp KHÁC giá trị băm lại từ chuỗi đang công bố kèm nó: "
                    + "chuỗi này không phải thứ đã được đóng dấu, tức phần trưng ra đã bị sửa sau khi đóng dấu.",
                    Expected: Hex.ChuanHoa(t.Digest),
                    Actual: tinhDuoc,
                    Preimage: t.Preimage)
                {
                    Metrics = soLieu,
                };
            }
        }

        // Bước 2: các dấu cùng một chuỗi đóng dấu là cùng một lần chốt (fan-out nhiều nơi cấp).
        var lanChot = moc
            .GroupBy(t => t.Preimage!, StringComparer.Ordinal)
            .Select(g => new LanChot(g.Key, g.ToList(), DocChuoiDongDau(g.Key)))
            .OrderByDescending(l => l.MoiNhat, StringComparer.Ordinal)
            .ToList();

        if (lanChot.FirstOrDefault(l => l.Truong is null) is { } la)
            return ChuaKiemDuoc(
                $"Chuỗi được {NoiCap(la.Dau[0])} đóng dấu không đúng định dạng {PhienBan} đang biết (thừa, thiếu "
                + "hoặc sai dòng), nên không bóc được từng trường ra để đối chiếu với dữ liệu đang công bố.",
                la.Preimage);

        var daSoSanh = lanChot.Select(l => (LanChot: l, KetQua: SoSanh(bc, l.Truong!))).ToList();
        var khop = daSoSanh.FirstOrDefault(x => x.KetQua.Khop);

        // Báo cáo công khai không công bố mã băm danh sách hồ sơ để đối chiếu (nó nằm ở bản dành cho
        // tổ giám sát) — vẫn phải hiện giá trị nằm trong dấu, để người có bản kia tự đối chiếu tay.
        if (string.IsNullOrWhiteSpace(bc.ListHash))
            soLieu.Add(new CheckMetric(
                "Mã băm danh sách hồ sơ trong dấu (báo cáo công khai không công bố để đối chiếu)",
                NguonNgauNhienChung.Co((khop.KetQua ?? daSoSanh[0].KetQua).MaBamDanhSachTrongDau)));

        if (khop.LanChot is not null) return Dat(khop.LanChot, lanChot, soLieu);

        // Không dấu nào khớp: điểm lệch (bằng chứng dữ liệu đã đổi) nặng hơn chỗ thiếu dữ liệu.
        var xau = daSoSanh.FirstOrDefault(x => x.KetQua.Lech.Count > 0);

        if (xau.LanChot is not null) return KhongDat(xau.LanChot, xau.KetQua, soLieu);

        var chuaDu = daSoSanh[0];

        return ChuaKiemDuoc(
            $"Chưa đối chiếu được {chuaDu.KetQua.Thieu.Count} trường của chuỗi đóng dấu với dữ liệu đang công bố — "
            + $"chỗ thiếu đầu tiên: {chuaDu.KetQua.Thieu[0]}. Những trường còn lại đều khớp, nhưng chừng nào còn "
            + "chỗ thiếu thì chưa kết luận được dấu này đóng lên đúng bộ số đang công bố.",
            chuaDu.LanChot.Preimage);
    }

    private static CheckResult Dat(LanChot khop, List<LanChot> lanChot, List<CheckMetric> soLieu)
    {
        var dau = khop.Dau[0];
        var giaiThich = DatGiaiThich;

        if (lanChot.Count > 1)
        {
            var soDauKhac = lanChot.Where(l => l != khop).Sum(l => l.Dau.Count);

            soLieu.Add(new CheckMetric("Số dấu đóng lên bộ số khác (chốt lại)", soDauKhac.ToString()));

            giaiThich += $" Báo cáo còn {soDauKhac} dấu đóng lên một bộ số khác — dấu của lần chốt lại trước đó. "
                         + "Chuyện này hợp lệ nếu buổi lễ có chốt lại trước khi mở cổng, nên phải đối chiếu với "
                         + "biên bản xem lần chốt lại đó có được ghi nhận hay không.";
        }

        return new CheckResult(
            CheckIds.FreezeTimestamp,
            Ten,
            CheckStatus.Dat,
            giaiThich,
            Expected: Hex.ChuanHoa(dau.Digest),
            Actual: Hex.Sha256Hex(Encoding.UTF8.GetBytes(khop.Preimage)),
            Preimage: khop.Preimage)
        {
            Metrics = soLieu,
        };
    }

    private static CheckResult KhongDat(LanChot lanChot, KetQuaSoSanh ketQua, List<CheckMetric> soLieu)
    {
        soLieu.Add(new CheckMetric("Số điểm lệch", ketQua.Lech.Count.ToString()));
        soLieu.AddRange(ketQua.Lech.Select((l, i) => new CheckMetric($"Điểm lệch {i + 1}", l.MoTa)));

        return new CheckResult(
            CheckIds.FreezeTimestamp,
            Ten,
            CheckStatus.KhongDat,
            $"Dấu thời gian đóng lên một bộ số KHÁC bộ số đang được công bố — {ketQua.Lech.Count} điểm lệch, điểm "
            + $"đầu tiên: {ketQua.Lech[0].MoTa}. Nghĩa là con số đang trưng ra không phải con số đã được bên thứ ba "
            + "chứng thực thời điểm, nên nó không còn bằng chứng nào cho biết nó có trước mốc neo."
            + (ketQua.Thieu.Count > 0
                ? $" (Ngoài ra còn {ketQua.Thieu.Count} trường chưa đối chiếu được vì báo cáo không công bố.)"
                : string.Empty),
            Expected: ketQua.Lech[0].KyVong,
            Actual: ketQua.Lech[0].ThucTe,
            Preimage: lanChot.Preimage)
        {
            Metrics = soLieu,
        };
    }

    /// <summary>Chuỗi đóng dấu chỉ lệch vì lỗi sao chép (xuống dòng Windows, mất dòng trống cuối)?</summary>
    private static bool LoiSaoChep(string preimage, string vanTay)
    {
        var lf = preimage.Replace("\r\n", "\n", StringComparison.Ordinal);

        return new[] { lf, preimage + "\n", lf + "\n" }
            .Any(u => string.Equals(Hex.Sha256Hex(Encoding.UTF8.GetBytes(u)), vanTay, StringComparison.Ordinal));
    }

    /// <summary>Một lần chốt: các dấu cùng đóng lên một chuỗi, đã bóc thành từng trường.</summary>
    private sealed record LanChot(string Preimage, List<TimestampToken> Dau, IReadOnlyDictionary<string, string>? Truong)
    {
        public string MoiNhat { get; } = Dau.Max(t => t.GenTime ?? string.Empty) ?? string.Empty;
    }

    private sealed record Lech(string MoTa, string KyVong, string ThucTe);

    private sealed record KetQuaSoSanh(
        List<Lech> Lech,
        List<string> Thieu,
        string MaBamDanhSachTrongDau)
    {
        public bool Khop => Lech.Count == 0 && Thieu.Count == 0;
    }

    /// <summary>
    /// Bóc chuỗi đóng dấu thành từng trường. Chỉ nhận đúng định dạng đã biết: dòng lạ, dòng lặp hay
    /// thiếu dòng đều trả <c>null</c> — đoán bừa ý nghĩa một định dạng chưa biết rồi kết luận ĐẠT
    /// mới là điều nguy hiểm, chứ không phải nói "không kiểm được".
    /// </summary>
    private static IReadOnlyDictionary<string, string>? DocChuoiDongDau(string preimage)
    {
        var dong = preimage.Split('\n');

        // Chuỗi kết thúc bằng "\n" ⇒ phần tử cuối rỗng; thiếu nó là đã sai định dạng.
        if (dong.Length != KhoaBatBuoc.Length + 2 || dong[^1].Length != 0) return null;
        if (!string.Equals(dong[0], PhienBan, StringComparison.Ordinal)) return null;

        var truong = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 1; i < dong.Length - 1; i++)
        {
            var dau = dong[i].IndexOf('=');
            if (dau <= 0) return null;

            var khoa = dong[i][..dau];
            if (!KhoaBatBuoc.Contains(khoa, StringComparer.Ordinal)) return null;
            if (!truong.TryAdd(khoa, dong[i][(dau + 1)..])) return null;
        }

        return truong.Count == KhoaBatBuoc.Length ? truong : null;
    }

    private static KetQuaSoSanh SoSanh(TransparencyReport bc, IReadOnlyDictionary<string, string> truong)
    {
        var lech = new List<Lech>();
        var thieu = new List<string>();

        void So(string khoa, string nhan, string? congBo, string thieuVi, Func<string, string, bool>? bang = null)
        {
            var trongDau = truong[khoa];

            if (string.IsNullOrWhiteSpace(congBo))
            {
                thieu.Add($"{nhan} ({khoa}): {thieuVi}");
                return;
            }

            var giaTri = congBo.Trim();
            var khop = bang?.Invoke(trongDau, giaTri)
                       ?? string.Equals(trongDau, giaTri, StringComparison.OrdinalIgnoreCase);

            if (!khop)
                lech.Add(new Lech(
                    $"{nhan} ({khoa}): trong dấu là «{MoTaGiaTri.Gon(trongDau)}», báo cáo đang công bố "
                    + $"«{MoTaGiaTri.Gon(giaTri)}»",
                    trongDau,
                    giaTri));
        }

        So("projectId", "định danh dự án", bc.ProjectId,
            "báo cáo không công bố định danh dự án", BangDinhDanh);

        // Mã băm danh sách chỉ có trong bản dành cho tổ giám sát; báo cáo công khai không công bố nên
        // không đối chiếu được ở đây (hạng mục danh sách hồ sơ mới là chỗ kiểm nó). Có thì đối chiếu.
        if (!string.IsNullOrWhiteSpace(bc.ListHash))
            So("listHash", "mã băm danh sách hồ sơ", bc.ListHash, "báo cáo không công bố mã băm danh sách hồ sơ");

        SoPhanGiamSat(bc, truong, lech, thieu);

        foreach (var vong in new[] { "A", "B", "C" })
        {
            var nguon = (bc.EntropySources ?? [])
                .Where(n => string.Equals(n?.Round?.Trim(), vong, StringComparison.OrdinalIgnoreCase))
                .ToList();

            So($"rServerCommit{vong}", $"cam kết ngẫu nhiên máy chủ vòng {vong}",
                nguon.Count == 1 ? nguon[0].RServerCommit : null,
                nguon.Count == 0
                    ? $"báo cáo không công bố nguồn ngẫu nhiên vòng {vong}"
                    : $"báo cáo công bố {nguon.Count} nguồn ngẫu nhiên cùng mang tên vòng {vong}, không rõ cái nào "
                      + "ứng với dấu");
        }

        var neo = bc.AnchorCommitment;

        So("ethTargetHeight", "mốc block đích Ethereum", Chuoi(neo?.EthTargetHeight),
            "báo cáo không công bố mốc block đích Ethereum của cam kết neo", BangSo);
        So("btcTargetHeight", "mốc block đích Bitcoin", Chuoi(neo?.BtcTargetHeight),
            "báo cáo không công bố mốc block đích Bitcoin của cam kết neo", BangSo);

        SoThoiDiemChot(neo?.AnchorFrozenAt, truong["anchorFrozenAt"], lech, thieu);

        return new KetQuaSoSanh(lech, thieu, truong["listHash"]);
    }

    /// <summary>
    /// Phần ngẫu nhiên của tổ giám sát công bố lặp lại ở từng vòng nhưng chỉ có một giá trị. Các vòng
    /// công bố khác nhau ⇒ báo cáo tự mâu thuẫn, không biết giá trị nào ứng với dấu để đối chiếu.
    /// </summary>
    private static void SoPhanGiamSat(
        TransparencyReport bc,
        IReadOnlyDictionary<string, string> truong,
        List<Lech> lech,
        List<string> thieu)
    {
        var giamSat = (bc.EntropySources ?? [])
            .Select(n => Hex.ChuanHoa(n?.RSupervisor))
            .Where(v => v is not null)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (giamSat.Count == 0)
        {
            thieu.Add("phần ngẫu nhiên của tổ giám sát (rSup): báo cáo không công bố");
            return;
        }

        if (giamSat.Count > 1)
        {
            thieu.Add("phần ngẫu nhiên của tổ giám sát (rSup): các vòng công bố giá trị khác nhau, không rõ giá "
                      + "trị nào ứng với dấu");
            return;
        }

        var trongDau = truong["rSup"];

        if (!string.Equals(trongDau, giamSat[0], StringComparison.OrdinalIgnoreCase))
            lech.Add(new Lech(
                $"phần ngẫu nhiên của tổ giám sát (rSup): trong dấu là «{MoTaGiaTri.Gon(trongDau)}», báo cáo đang "
                + $"công bố «{MoTaGiaTri.Gon(giamSat[0]!)}»",
                trongDau,
                giamSat[0]!));
    }

    /// <summary>
    /// So GIÁ TRỊ thời điểm, không so chuỗi ký tự: chuỗi đóng dấu dùng đúng 3 chữ số lẻ giây còn báo
    /// cáo JSON hiện 7, nên so chuỗi thì mọi báo cáo thật đều KHÔNG ĐẠT oan.
    /// </summary>
    private static void SoThoiDiemChot(string? congBo, string trongDau, List<Lech> lech, List<string> thieu)
    {
        const string Nhan = "thời điểm chốt mốc neo (anchorFrozenAt)";

        if (string.IsNullOrWhiteSpace(congBo))
        {
            thieu.Add($"{Nhan}: báo cáo không công bố");
            return;
        }

        var mocCongBo = DauThoiGianChung.DocMoc(congBo);

        if (mocCongBo is null)
        {
            thieu.Add($"{Nhan}: giá trị đang công bố không phải mốc thời gian đọc được");
            return;
        }

        var mocTrongDau = DauThoiGianChung.DocMoc(trongDau);

        if (mocTrongDau != mocCongBo)
            lech.Add(new Lech(
                $"{Nhan}: trong dấu là «{MoTaGiaTri.Gon(trongDau)}», báo cáo đang công bố "
                + $"«{MoTaGiaTri.Gon(congBo.Trim())}»",
                trongDau,
                congBo.Trim()));
    }

    private static bool BangDinhDanh(string trongDau, string congBo) =>
        Guid.TryParse(trongDau, out var a) && Guid.TryParse(congBo, out var b)
            ? a == b
            : string.Equals(trongDau, congBo, StringComparison.OrdinalIgnoreCase);

    private static bool BangSo(string trongDau, string congBo) =>
        long.TryParse(trongDau, NumberStyles.Integer, CultureInfo.InvariantCulture, out var a)
        && long.TryParse(congBo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var b)
        && a == b;

    private static string? Chuoi(long? giaTri) => giaTri?.ToString(CultureInfo.InvariantCulture);

    private static string NoiCap(TimestampToken dau) =>
        string.IsNullOrWhiteSpace(dau.Authority) ? NhanKhongRoNoiCap : dau.Authority.Trim();
}
