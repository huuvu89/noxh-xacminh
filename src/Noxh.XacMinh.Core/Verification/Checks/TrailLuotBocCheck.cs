using System.Globalization;
using Noxh.XacMinh.Core.Kho;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Hạng mục 12: từng lượt bốc trên trail bằng chứng đối chiếu với nhật ký bốc đã công bố.
///
/// Hai chiều lệch có sức nặng khác hẳn nhau, nên không được gộp:
///  · <b>Trail có mà báo cáo thiếu</b> (hoặc cùng một ô phiếu mà hai bên khai khác nhau) — KHÔNG ĐẠT.
///    Bản ghi đã nằm trên kho chỉ-ghi từ lúc lễ chạy thì không sửa lại được nữa; báo cáo công bố sau
///    lại nói khác đi nghĩa là dữ liệu đã bị đổi ở khoảng giữa.
///  · <b>Báo cáo có mà trail thiếu</b> — chỉ KHÔNG KIỂM ĐƯỢC. Đường đẩy bằng chứng là best-effort có
///    chủ ý (hàng đợi đầy thì máy chủ bỏ bản ghi chứ không chặn lượt bốc), và vé do <b>máy bốc thay</b>
///    thì không đi qua đường bốc vé nên không bao giờ lên trail. Biến chỗ đó thành lời buộc tội là vu
///    oan cho một hàng đợi đầy.
/// </summary>
internal static class TrailLuotBocCheck
{
    private const string Title = "Trail bằng chứng — lượt bốc đối chiếu với nhật ký công bố";

    private const string MoTaLoai = "lượt bốc";

    /// <summary>Số ca nêu đích danh trong câu giải thích — nhiều hơn thì người đọc không đọc nữa.</summary>
    private const int SoCaNeuDich = 3;

    public static IEnumerable<CheckResult> Run(VerificationInput input)
    {
        yield return Kiem(input.Kho, input.Report);
    }

    /// <summary>Một lượt bốc trên trail đã bóc ra các trường cần đối chiếu.</summary>
    private sealed record LuotTrail(
        string LoKey, string? Vong, string? MaHoSo, string? MaChongPhieu, long? ViTri, string? NoiDungVe)
    {
        public string? Khoa => MaChongPhieu is { } deck && !string.IsNullOrWhiteSpace(deck) && ViTri is { } vt
            ? $"{deck.Trim().ToLowerInvariant()}#{vt.ToString(CultureInfo.InvariantCulture)}"
            : null;

        public string MoTa => $"chồng phiếu {MaChongPhieu}, vị trí {ViTri}";
    }

    private static CheckResult Kiem(KhoBangChung? kho, TransparencyReport bao)
    {
        CheckResult ChuaKiemDuoc(string vi, int soBanGhi = 0) =>
            new(CheckIds.TrailLuotBoc, Title, CheckStatus.KhongKiemDuoc, vi)
            {
                Metrics = TrailDoiChieuChung.SoLieu(kho, MoTaLoai, soBanGhi),
            };

        if (TrailDoiChieuChung.CuaVao(kho) is { } cua) return ChuaKiemDuoc(cua);

        var tap = TrailDoiChieuChung.Doc(kho!, TrailDoiChieuChung.LoaiLuotBoc, bao.ProjectId);
        if (tap.CuaDuAn.Count == 0)
            return ChuaKiemDuoc(TrailDoiChieuChung.ViSaoKhongCoBanGhi(tap, MoTaLoai));

        var nhatKy = bao.DrawLog;
        if (nhatKy is null || nhatKy.Count == 0)
            return ChuaKiemDuoc(
                "Báo cáo không công bố nhật ký bốc, nên không có gì để đem so với các lượt bốc đọc được trên "
                + $"trail. {TrailDoiChieuChung.GioiHan}",
                tap.CuaDuAn.Count);

        return DoiChieu(kho!, tap, nhatKy);
    }

    private static CheckResult DoiChieu(
        KhoBangChung kho, TrailDoiChieuChung.TapBanGhi tap, IReadOnlyList<DrawLogEntry> nhatKy)
    {
        var trail = tap.DocDuoc.Select(Doc).ToList();
        var khongCoKhoa = trail.Count(l => l.Khoa is null);

        var theoKhoa = nhatKy
            .Select(e => (Khoa: KhoaCua(e), Buoc: e))
            .Where(x => x.Khoa is not null)
            .GroupBy(x => x.Khoa!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Buoc, StringComparer.Ordinal);

        var baoCaoKhongCoKhoa = nhatKy.Count(e => KhoaCua(e) is null);

        var thieuTrongBaoCao = new List<string>();
        var khacNoiDung = new List<string>();
        var khopDuoc = new HashSet<string>(StringComparer.Ordinal);
        var thieuTruong = 0;

        foreach (var nhom in trail.Where(l => l.Khoa is not null).GroupBy(l => l.Khoa!, StringComparer.Ordinal))
        {
            // Đẩy lại cùng một bản ghi (upload retry) là chuyện bình thường; hai bản ghi cùng một ô
            // phiếu mà khai khác nhau thì chính trail đã tự mâu thuẫn — đó là một phát hiện thật.
            var khacNhau = nhom.Select(l => (l.Vong, l.MaHoSo, l.ViTri, l.NoiDungVe)).Distinct().Count() > 1;
            if (khacNhau)
            {
                khacNoiDung.Add($"{nhom.First().MoTa}: chính trail có hai bản ghi khai khác nhau cho cùng ô phiếu");
                continue;
            }

            var lo = nhom.First();

            if (!theoKhoa.TryGetValue(nhom.Key, out var buoc))
            {
                thieuTrongBaoCao.Add(lo.MoTa);
                continue;
            }

            khopDuoc.Add(nhom.Key);

            var lech = SoSanh(lo, buoc, ref thieuTruong);
            if (lech is not null) khacNoiDung.Add($"{lo.MoTa}: {lech}");
        }

        var chuaLenTrail = nhatKy
            .Where(e => KhoaCua(e) is { } k && !khopDuoc.Contains(k) && e.AutoDrawn != true)
            .ToList();
        var mayBocThay = nhatKy.Count(e => e.AutoDrawn == true);

        var soLieu = TrailDoiChieuChung.SoLieu(kho, MoTaLoai, tap.CuaDuAn.Count);
        soLieu.Add(new CheckMetric("Số lượt bốc trong báo cáo", nhatKy.Count.ToString(CultureInfo.InvariantCulture)));
        soLieu.Add(new CheckMetric("Số lượt khớp cả hai nơi",
            khopDuoc.Count.ToString(CultureInfo.InvariantCulture)));
        soLieu.Add(new CheckMetric("Số vé máy bốc thay (không lên trail, đúng thiết kế)",
            mayBocThay.ToString(CultureInfo.InvariantCulture)));

        if (thieuTrongBaoCao.Count > 0 || khacNoiDung.Count > 0)
            return Lech(soLieu, thieuTrongBaoCao, khacNoiDung);

        var chuaKetLuanDuoc = ChuaKetLuanDuoc(
            kho, tap, chuaLenTrail.Count, khongCoKhoa, baoCaoKhongCoKhoa, thieuTruong);

        if (chuaKetLuanDuoc is not null)
            return new CheckResult(CheckIds.TrailLuotBoc, Title, CheckStatus.KhongKiemDuoc, chuaKetLuanDuoc)
            {
                Metrics = soLieu,
            };

        return new CheckResult(
            CheckIds.TrailLuotBoc,
            Title,
            CheckStatus.Dat,
            $"Cả {khopDuoc.Count} lượt bốc đọc được trên trail đều có trong nhật ký bốc đã công bố, và khai đúng "
            + "cùng một chuyện: cùng hồ sơ, cùng vòng, cùng ô phiếu, cùng nội dung vé. Bản trên trail được đẩy "
            + "thẳng lên kho chỉ-ghi ngay trong lúc lễ chạy, nên nó không sửa lại được nữa — hai bản khớp nhau "
            + "nghĩa là nhật ký công bố sau lễ không bị sửa ở khoảng giữa. "
            + (mayBocThay > 0
                ? $"({mayBocThay} vé do máy bốc thay không có trên trail, đúng thiết kế: vé đó sinh ở lúc đóng "
                  + "vòng, không đi qua đường bốc vé.) "
                : string.Empty)
            + TrailDoiChieuChung.GioiHan,
            Expected: $"{khopDuoc.Count} lượt khớp",
            Actual: $"{khopDuoc.Count} lượt khớp")
        {
            Metrics = soLieu,
        };
    }

    private static CheckResult Lech(
        List<CheckMetric> soLieu, List<string> thieuTrongBaoCao, List<string> khacNoiDung)
    {
        var cau = new List<string>();

        if (thieuTrongBaoCao.Count > 0)
            cau.Add($"{thieuTrongBaoCao.Count} lượt bốc CÓ trên trail nhưng KHÔNG có trong nhật ký bốc đã công bố "
                    + $"({NeuDich(thieuTrongBaoCao)})");

        if (khacNoiDung.Count > 0)
            cau.Add($"{khacNoiDung.Count} lượt bốc có ở cả hai nơi nhưng khai KHÁC nhau ({NeuDich(khacNoiDung)})");

        return new CheckResult(
            CheckIds.TrailLuotBoc,
            Title,
            CheckStatus.KhongDat,
            $"Trail và báo cáo nói khác nhau: {string.Join("; ", cau)}. Bản ghi trên trail đã lên kho chỉ-ghi "
            + "ngay trong lúc lễ chạy nên không sửa được nữa — báo cáo công bố sau lại khác đi nghĩa là dữ liệu "
            + "đã bị đổi ở khoảng giữa, hoặc bản báo cáo bạn đang xem không phải bản của buổi lễ này. Hãy đối "
            + $"chiếu thêm với biên bản buổi lễ trước khi kết luận nguyên nhân. {TrailDoiChieuChung.GioiHan}",
            Expected: "trail và nhật ký công bố khai giống nhau",
            Actual: string.Join("; ", cau))
        {
            Metrics = soLieu,
        };
    }

    /// <summary>Vì sao chưa dám kết luận ĐẠT — mỗi lý do là một chỗ công cụ nhìn không hết.</summary>
    private static string? ChuaKetLuanDuoc(
        KhoBangChung kho,
        TrailDoiChieuChung.TapBanGhi tap,
        int chuaLenTrail,
        int khongCoKhoa,
        int baoCaoKhongCoKhoa,
        int thieuTruong)
    {
        var vi = new List<string>();

        if (!kho.DaLietKeHet)
            vi.Add("danh sách lô đọc được còn dở (kho còn trang chưa đọc hết), nên lượt bốc thiếu ở đây chưa nói "
                   + "lên điều gì");

        if (tap.SoKhongBocDuoc > 0)
            vi.Add($"{tap.SoKhongBocDuoc} bản ghi trên trail không bóc được nội dung");

        if (khongCoKhoa > 0)
            vi.Add($"{khongCoKhoa} bản ghi trên trail không khai đủ chồng phiếu và vị trí phiếu nên không ghép "
                   + "được với nhật ký");

        if (baoCaoKhongCoKhoa > 0)
            vi.Add($"{baoCaoKhongCoKhoa} lượt bốc trong báo cáo không khai đủ chồng phiếu và vị trí phiếu");

        if (thieuTruong > 0)
            vi.Add($"{thieuTruong} trường không có ở một bên nên không đem so được");

        if (chuaLenTrail > 0)
            vi.Add($"{chuaLenTrail} lượt bốc có trong báo cáo mà KHÔNG thấy trên trail — chuyện này có hai cách "
                   + "giải thích và công cụ không chọn hộ: đường đẩy bằng chứng là best-effort (hàng đợi đầy thì "
                   + "máy chủ bỏ bản ghi chứ không chặn lượt bốc, và đường đẩy có thể chưa bật lúc đó), hoặc lượt "
                   + "bốc đó được thêm vào nhật ký sau lễ");

        return vi.Count == 0
            ? null
            : $"Các lượt bốc đối chiếu được thì khớp, nhưng chưa kết luận được cho cả nhật ký: "
              + $"{string.Join("; ", vi)}. {TrailDoiChieuChung.GioiHan}";
    }

    private static string NeuDich(List<string> ca) =>
        string.Join("; ", ca.Take(SoCaNeuDich))
        + (ca.Count > SoCaNeuDich ? $"; và {ca.Count - SoCaNeuDich} ca nữa" : string.Empty);

    /// <summary>Ô phiếu là khoá ghép hai bên: backend đảm bảo (chồng phiếu, vị trí) là duy nhất.</summary>
    private static string? KhoaCua(DrawLogEntry e) =>
        !string.IsNullOrWhiteSpace(e.DeckId) && e.Position is { } vt
            ? $"{e.DeckId.Trim().ToLowerInvariant()}#{vt.ToString(CultureInfo.InvariantCulture)}"
            : null;

    private static string? SoSanh(LuotTrail lo, DrawLogEntry buoc, ref int thieuTruong)
    {
        var lech = new List<string>();

        if (string.IsNullOrWhiteSpace(lo.MaHoSo) || string.IsNullOrWhiteSpace(buoc.ApplicantId)) thieuTruong++;
        else if (!string.Equals(lo.MaHoSo.Trim(), buoc.ApplicantId.Trim(), StringComparison.OrdinalIgnoreCase))
            lech.Add($"trail khai hồ sơ «{MoTaGiaTri.Gon(lo.MaHoSo.Trim())}», báo cáo khai "
                     + $"«{MoTaGiaTri.Gon(buoc.ApplicantId.Trim())}»");

        if (string.IsNullOrWhiteSpace(lo.NoiDungVe) || buoc.Payload is null) thieuTruong++;
        else if (!string.Equals(lo.NoiDungVe, buoc.Payload, StringComparison.Ordinal))
            lech.Add($"trail khai nội dung vé «{MoTaGiaTri.Gon(lo.NoiDungVe)}», báo cáo khai "
                     + $"«{MoTaGiaTri.Gon(buoc.Payload)}»");

        if (string.IsNullOrWhiteSpace(lo.Vong) || string.IsNullOrWhiteSpace(buoc.Round)) thieuTruong++;
        else if (!string.Equals(lo.Vong.Trim(), buoc.Round.Trim(), StringComparison.Ordinal))
            lech.Add($"trail khai vòng «{MoTaGiaTri.Gon(lo.Vong.Trim())}», báo cáo khai "
                     + $"«{MoTaGiaTri.Gon(buoc.Round.Trim())}»");

        return lech.Count == 0 ? null : string.Join(", ", lech);
    }

    private static LuotTrail Doc(TrailDoiChieuChung.BanGhiDoiChieu ban) =>
        new(ban.LoKey,
            TrailDoiChieuChung.Chuoi(ban.Payload, "round"),
            TrailDoiChieuChung.Chuoi(ban.Payload, "applicantId"),
            TrailDoiChieuChung.Chuoi(ban.Payload, "deckId"),
            TrailDoiChieuChung.So(ban.Payload, "position"),
            TrailDoiChieuChung.Chuoi(ban.Payload, "ticketPayload"));
}
