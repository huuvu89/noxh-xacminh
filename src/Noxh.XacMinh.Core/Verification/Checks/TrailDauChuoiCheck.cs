using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Kho;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Hạng mục 14: đầu chuỗi băm của từng vòng trên trail bằng chứng đối chiếu với đầu chuỗi
/// <b>tính lại được</b> từ nhật ký bốc đã công bố.
///
/// Đây là chỗ trail có sức nặng riêng. Hạng mục "chuỗi băm nhật ký bốc" chỉ nói được nhật ký đang
/// công bố có tự nhất quán hay không; nó không chặn được người sửa cả nhật ký rồi tính lại cả chuỗi
/// băm cho khớp, vì chuỗi ấy sống trong chính cơ sở dữ liệu bị sửa. Đầu chuỗi từng vòng thì đã được
/// đẩy lên kho chỉ-ghi <b>ngay khi vòng đóng</b>, nên tính lại chuỗi từ nhật ký công bố mà không ra
/// đúng con số ấy nghĩa là nhật ký đã đổi sau khi vòng đóng.
///
/// Đầu chuỗi là giá trị <b>cộng dồn</b> toàn dự án tại lúc vòng đó đóng (A1 → A2 → B → C), đúng cách
/// backend materialize chuỗi, nên vòng nào cũng ghim luôn mọi vòng trước nó.
/// </summary>
internal static class TrailDauChuoiCheck
{
    private const string Title = "Trail bằng chứng — đầu chuỗi băm từng vòng";

    private const string MoTaLoai = "đầu chuỗi băm";

    /// <summary>
    /// Nhãn vòng backend ghim ở mốc đóng vòng ↔ thứ tự vòng khi duyệt chuỗi. Nhãn ở đây là nhãn
    /// <b>mốc</b> (<c>A2</c>), khác nhãn vòng trong nhật ký bốc (<c>A2:2PN</c>) — nên không dùng
    /// chung phép ánh xạ với nhật ký được.
    /// </summary>
    private static readonly Dictionary<string, int> ThuTuVongMoc = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A1"] = 0,
        ["A2"] = 1,
        ["B"] = 2,
        ["C"] = 3,
    };

    public static IEnumerable<CheckResult> Run(VerificationInput input)
    {
        yield return Kiem(input.Kho, input.Report);
    }

    private static CheckResult Kiem(KhoBangChung? kho, TransparencyReport bao)
    {
        CheckResult ChuaKiemDuoc(string vi, int soBanGhi = 0, List<CheckMetric>? soLieu = null) =>
            new(CheckIds.TrailDauChuoi, Title, CheckStatus.KhongKiemDuoc, vi)
            {
                Metrics = soLieu ?? TrailDoiChieuChung.SoLieu(kho, MoTaLoai, soBanGhi),
            };

        if (TrailDoiChieuChung.CuaVao(kho) is { } cua) return ChuaKiemDuoc(cua);

        var tap = TrailDoiChieuChung.Doc(kho!, TrailDoiChieuChung.LoaiDauChuoi, bao.ProjectId);
        if (tap.CuaDuAn.Count == 0)
            return ChuaKiemDuoc(TrailDoiChieuChung.ViSaoKhongCoBanGhi(tap, MoTaLoai));

        var nhatKy = bao.DrawLog;
        if (nhatKy is null || nhatKy.Count == 0)
            return ChuaKiemDuoc(
                "Báo cáo không công bố nhật ký bốc, nên không tính lại được đầu chuỗi băm nào để đem so với giá "
                + $"trị đã ghim trên trail. {TrailDoiChieuChung.GioiHan}",
                tap.CuaDuAn.Count);

        var buoc = NhatKyBocChung.DocVaSapXep(nhatKy);
        if (buoc.FirstOrDefault(b => !b.DocDuoc) is { } thieu)
            return ChuaKiemDuoc(
                $"Không tính lại được chuỗi băm từ nhật ký công bố nên chưa đối chiếu được đầu chuỗi: {thieu.Loi} "
                + TrailDoiChieuChung.GioiHan,
                tap.CuaDuAn.Count);

        return DoiChieu(kho!, tap, buoc);
    }

    private static CheckResult DoiChieu(
        KhoBangChung kho, TrailDoiChieuChung.TapBanGhi tap, List<NhatKyBocChung.Buoc> buoc)
    {
        var dauChuoi = NhatKyBocChung.TinhLaiChuoi(buoc);
        var soLieu = TrailDoiChieuChung.SoLieu(kho, MoTaLoai, tap.CuaDuAn.Count);

        var lech = new List<string>();
        var chuaSoDuoc = new List<string>();
        var daSo = new List<string>();

        var khongKhaiVong = tap.DocDuoc.Select(Doc).Count(m => string.IsNullOrWhiteSpace(m.Vong));
        if (khongKhaiVong > 0)
            chuaSoDuoc.Add($"{khongKhaiVong} bản ghi đầu chuỗi trên trail không khai vòng nào");

        foreach (var nhom in tap.DocDuoc.Select(Doc)
                     .Where(m => !string.IsNullOrWhiteSpace(m.Vong))
                     .GroupBy(m => m.Vong!.Trim(), StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            // Đẩy lại cùng một mốc là chuyện bình thường; hai bản ghi cùng một vòng mà ghim đầu chuỗi
            // khác nhau thì chính trail đã tự mâu thuẫn — không được im lặng chọn lấy một bản.
            if (nhom.Select(m => (Hex.ChuanHoa(m.DauChuoi), m.SoBuoc)).Distinct().Count() > 1)
            {
                lech.Add($"vòng {nhom.Key}: chính trail có hai bản ghi ghim đầu chuỗi khác nhau");
                continue;
            }

            var moc = nhom.First();

            if (!ThuTuVongMoc.TryGetValue(nhom.Key, out var thuTu))
            {
                chuaSoDuoc.Add($"vòng «{MoTaGiaTri.Gon(nhom.Key)}» trên trail không phải một vòng công cụ biết");
                continue;
            }

            var ghim = Hex.ChuanHoa(moc.DauChuoi);
            if (ghim is null)
            {
                chuaSoDuoc.Add($"vòng {nhom.Key} trên trail không ghim đầu chuỗi hợp lệ");
                continue;
            }

            // Đầu chuỗi cộng dồn: mọi bước thuộc vòng này và các vòng trước nó.
            var soBuocTinhDuoc = buoc.Count(b => b.ThuTuVong <= thuTu);
            if (soBuocTinhDuoc == 0)
            {
                chuaSoDuoc.Add($"nhật ký công bố không có lượt bốc nào thuộc vòng {nhom.Key} hay vòng trước nó");
                continue;
            }

            var tinhDuoc = dauChuoi[soBuocTinhDuoc - 1];
            soLieu.Add(new CheckMetric($"Đầu chuỗi vòng {nhom.Key} trên trail", ghim));

            if (!string.Equals(ghim, tinhDuoc, StringComparison.Ordinal))
            {
                lech.Add($"vòng {nhom.Key}: trail ghim «{ghim}», tính lại từ nhật ký công bố ra «{tinhDuoc}»");
                continue;
            }

            if (moc.SoBuoc is { } khai && khai != soBuocTinhDuoc)
            {
                lech.Add($"vòng {nhom.Key}: đầu chuỗi khớp nhưng trail khai {khai} bước còn nhật ký công bố có "
                         + $"{soBuocTinhDuoc} bước tính tới hết vòng này");
                continue;
            }

            daSo.Add(nhom.Key);
        }

        if (lech.Count > 0)
            return new CheckResult(
                CheckIds.TrailDauChuoi,
                Title,
                CheckStatus.KhongDat,
                $"Đầu chuỗi băm tính lại từ nhật ký đang công bố KHÁC giá trị đã ghim lên kho chỉ-ghi lúc đóng "
                + $"vòng: {string.Join("; ", lech)}. Giá trị trên trail lên kho ngay khi vòng đóng nên không sửa "
                + "lại được — nhật ký công bố sau lễ tính ra con số khác nghĩa là nhật ký đã bị đổi sau khi vòng "
                + "đóng. Vòng lệch sớm nhất là chỗ đáng soi trước, vì đầu chuỗi cộng dồn nên mọi vòng sau nó cũng "
                + $"lệch theo. {TrailDoiChieuChung.GioiHan}",
                Expected: "đầu chuỗi ghim trên trail",
                Actual: string.Join("; ", lech))
            {
                Metrics = soLieu,
            };

        if (ChuaKetLuanDuoc(kho, tap, daSo, chuaSoDuoc) is { } chua)
            return new CheckResult(CheckIds.TrailDauChuoi, Title, CheckStatus.KhongKiemDuoc, chua)
            {
                Metrics = soLieu,
            };

        return new CheckResult(
            CheckIds.TrailDauChuoi,
            Title,
            CheckStatus.Dat,
            $"Đầu chuỗi băm của {daSo.Count} vòng ({string.Join(", ", daSo)}) tính lại được từ nhật ký bốc đang "
            + "công bố đúng bằng giá trị đã đẩy lên kho chỉ-ghi ngay lúc vòng đó đóng. Đây là chỗ trail nói được "
            + "điều mà chuỗi băm trong cơ sở dữ liệu không nói được: sửa cả nhật ký rồi tính lại cả chuỗi băm cho "
            + "tự khớp thì vẫn không đổi được con số đã nằm trên kho, nên nhật ký không bị sửa sau khi vòng đóng. "
            + TrailDoiChieuChung.GioiHan,
            Expected: string.Join(", ", daSo),
            Actual: string.Join(", ", daSo))
        {
            Metrics = soLieu,
        };
    }

    private static string? ChuaKetLuanDuoc(
        KhoBangChung kho, TrailDoiChieuChung.TapBanGhi tap, List<string> daSo, List<string> chuaSoDuoc)
    {
        var vi = new List<string>(chuaSoDuoc);

        if (tap.SoKhongBocDuoc > 0)
            vi.Add($"{tap.SoKhongBocDuoc} bản ghi đầu chuỗi trên trail không bóc được nội dung");

        if (!kho.DaLietKeHet)
            vi.Add("danh sách lô đọc được còn dở nên có thể còn mốc vòng chưa đọc tới");

        if (daSo.Count == 0)
            return $"Chưa đối chiếu được đầu chuỗi của vòng nào: {string.Join("; ", vi)}. "
                   + TrailDoiChieuChung.GioiHan;

        return vi.Count == 0
            ? null
            : $"Đầu chuỗi của {daSo.Count} vòng đối chiếu được thì khớp, nhưng chưa kết luận được cho cả buổi lễ: "
              + $"{string.Join("; ", vi)}. {TrailDoiChieuChung.GioiHan}";
    }

    /// <summary>Một mốc đầu chuỗi đọc được từ trail.</summary>
    private sealed record MocDauChuoi(string? Vong, long? SoBuoc, string? DauChuoi);

    private static MocDauChuoi Doc(TrailDoiChieuChung.BanGhiDoiChieu ban) =>
        new(TrailDoiChieuChung.Chuoi(ban.Payload, "round"),
            TrailDoiChieuChung.So(ban.Payload, "stepCount"),
            TrailDoiChieuChung.Chuoi(ban.Payload, "headHex"));
}
