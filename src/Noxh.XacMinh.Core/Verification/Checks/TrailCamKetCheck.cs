using System.Globalization;
using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Kho;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Hạng mục 13: cam kết ngẫu nhiên máy chủ trên trail bằng chứng đối chiếu với cam kết đã công bố.
///
/// Cam kết là thứ ràng máy chủ lại: nó phải được niêm phong <b>trước</b> khi máy chủ biết mốc neo, và
/// hạng mục "cam kết ngẫu nhiên máy chủ" đã kiểm <c>SHA-256(rServer) == rServerCommit</c>. Nhưng cả
/// hai vế đó đều lấy từ <b>cùng một</b> báo cáo — ai sửa được cơ sở dữ liệu trước lúc công bố thì sửa
/// cả cặp là xong. Bản cam kết trên trail được đẩy lên kho chỉ-ghi ngay lúc tổ giám sát chốt entropy,
/// nên nó là bản đối chứng nằm ngoài tầm với đó.
///
/// Chốt lại (refreeze) là thao tác hợp lệ, nên <b>lần chốt sau cùng</b> mới là lần ràng buộc dữ liệu
/// đang công bố — báo cáo khớp một lần chốt cũ thì phải nói thẳng ra là khớp lần nào.
/// </summary>
internal static class TrailCamKetCheck
{
    private const string Title = "Trail bằng chứng — cam kết ngẫu nhiên máy chủ";

    private const string MoTaLoai = "cam kết ngẫu nhiên";

    /// <summary>Vòng entropy trong báo cáo ↔ trường cam kết trên trail; backend chốt cả ba một lần.</summary>
    private static readonly (string Vong, string Truong)[] TheoVong =
    [
        ("A", "rServerCommitA"),
        ("B", "rServerCommitB"),
        ("C", "rServerCommitC"),
    ];

    public static IEnumerable<CheckResult> Run(VerificationInput input)
    {
        yield return Kiem(input.Kho, input.Report);
    }

    private static CheckResult Kiem(KhoBangChung? kho, TransparencyReport bao)
    {
        CheckResult ChuaKiemDuoc(string vi, int soBanGhi = 0, List<CheckMetric>? soLieu = null) =>
            new(CheckIds.TrailCamKet, Title, CheckStatus.KhongKiemDuoc, vi)
            {
                Metrics = soLieu ?? TrailDoiChieuChung.SoLieu(kho, MoTaLoai, soBanGhi),
            };

        if (TrailDoiChieuChung.CuaVao(kho) is { } cua) return ChuaKiemDuoc(cua);

        var tap = TrailDoiChieuChung.Doc(kho!, TrailDoiChieuChung.LoaiCamKet, bao.ProjectId);
        if (tap.CuaDuAn.Count == 0)
            return ChuaKiemDuoc(TrailDoiChieuChung.ViSaoKhongCoBanGhi(tap, MoTaLoai));

        var nguon = bao.EntropySources;
        if (nguon is null || nguon.Count == 0)
            return ChuaKiemDuoc(
                "Báo cáo không công bố nguồn ngẫu nhiên vòng nào, nên không có cam kết nào để đem so với bản cam "
                + $"kết đọc được trên trail. {TrailDoiChieuChung.GioiHan}",
                tap.CuaDuAn.Count);

        // Lần chốt sau cùng là lần ràng buộc dữ liệu đang công bố; số lần chốt tăng dần nên nó cũng
        // là thước đo "có ai chốt lại nhiều bất thường không".
        var moiLanChot = tap.DocDuoc.Select(Doc).OrderBy(c => c.LanChot ?? 0).ToList();
        if (moiLanChot.Count == 0)
            return ChuaKiemDuoc(
                $"Có {tap.SoKhongBocDuoc} bản ghi cam kết trên trail nhưng không bóc được nội dung, nên chưa đối "
                + $"chiếu được gì. {TrailDoiChieuChung.GioiHan}",
                tap.CuaDuAn.Count);

        var sauCung = moiLanChot[^1];

        var soLieu = TrailDoiChieuChung.SoLieu(kho, MoTaLoai, tap.CuaDuAn.Count);
        soLieu.Add(new CheckMetric("Số lần chốt entropy thấy trên trail",
            moiLanChot.Count.ToString(CultureInfo.InvariantCulture)));

        var lech = new List<string>();
        var chuaSoDuoc = new List<string>();
        var khop = 0;

        foreach (var (vong, truong) in TheoVong)
        {
            var congBo = Hex.ChuanHoa(nguon
                .FirstOrDefault(n => string.Equals(n?.Round?.Trim(), vong, StringComparison.OrdinalIgnoreCase))
                ?.RServerCommit);
            var trenTrail = Hex.ChuanHoa(sauCung.CamKet(truong));

            soLieu.Add(new CheckMetric($"Cam kết vòng {vong} trên trail",
                trenTrail ?? NguonNgauNhienChung.KhongCo));

            if (congBo is null || trenTrail is null)
            {
                chuaSoDuoc.Add(congBo is null
                    ? $"vòng {vong} không có cam kết trong báo cáo"
                    : $"vòng {vong} không có cam kết trên trail");
                continue;
            }

            if (string.Equals(congBo, trenTrail, StringComparison.Ordinal))
            {
                khop++;
                continue;
            }

            var khopLanChotCu = moiLanChot
                .Where(c => string.Equals(Hex.ChuanHoa(c.CamKet(truong)), congBo, StringComparison.Ordinal))
                .Select(c => c.LanChot)
                .FirstOrDefault();

            lech.Add($"vòng {vong}: trail ghim «{trenTrail}», báo cáo công bố «{congBo}»"
                     + (khopLanChotCu is { } lan
                         ? $" (giá trị báo cáo đang công bố khớp lần chốt thứ {lan} trên trail, không phải lần "
                           + "chốt sau cùng)"
                         : string.Empty));
        }

        if (lech.Count > 0)
            return new CheckResult(
                CheckIds.TrailCamKet,
                Title,
                CheckStatus.KhongDat,
                $"Cam kết ngẫu nhiên máy chủ đang công bố KHÁC bản đã đẩy lên kho chỉ-ghi lúc chốt entropy: "
                + $"{string.Join("; ", lech)}. Bản trên trail lên kho trước cả khi vòng đầu tiên mở, nên nó "
                + "không sửa lại được — cặp (phần ngẫu nhiên, cam kết) đang công bố có tự khớp với nhau cũng "
                + "không cứu được điều đó, vì cả hai đều lấy từ cùng một cơ sở dữ liệu. "
                + TrailDoiChieuChung.GioiHan,
                Expected: sauCung.CamKet(TheoVong[0].Truong),
                Actual: string.Join("; ", lech))
            {
                Metrics = soLieu,
            };

        if (khop == 0 || chuaSoDuoc.Count > 0)
            return ChuaKiemDuoc(
                (khop > 0 ? $"{khop} vòng đối chiếu được thì khớp, nhưng còn " : "Chưa đối chiếu được vòng nào: ")
                + $"{string.Join("; ", chuaSoDuoc)}. {TrailDoiChieuChung.GioiHan}",
                soLieu: soLieu);

        return new CheckResult(
            CheckIds.TrailCamKet,
            Title,
            CheckStatus.Dat,
            $"Cam kết ngẫu nhiên máy chủ của cả {khop} vòng đang công bố đúng bằng bản đã đẩy lên kho chỉ-ghi "
            + "lúc tổ giám sát chốt entropy — bản đó lên kho trước khi vòng đầu tiên mở và không sửa lại được. "
            + "Nghĩa là cam kết ràng máy chủ không bị thay sau khi lễ đã chạy, kể cả bởi người nắm cơ sở dữ liệu. "
            + (moiLanChot.Count > 1
                ? $"(Trail ghi nhận {moiLanChot.Count} lần chốt entropy; giá trị đem so là lần chốt sau cùng.) "
                : string.Empty)
            + TrailDoiChieuChung.GioiHan,
            Expected: sauCung.CamKet(TheoVong[0].Truong),
            Actual: Hex.ChuanHoa(nguon.FirstOrDefault(n =>
                string.Equals(n?.Round?.Trim(), TheoVong[0].Vong, StringComparison.OrdinalIgnoreCase))?.RServerCommit))
        {
            Metrics = soLieu,
        };
    }

    /// <summary>Một lần chốt entropy đọc được từ trail.</summary>
    private sealed record LanChotTrail(long? LanChot, string? A, string? B, string? C)
    {
        public string? CamKet(string truong) => truong switch
        {
            "rServerCommitA" => A,
            "rServerCommitB" => B,
            _ => C,
        };
    }

    private static LanChotTrail Doc(TrailDoiChieuChung.BanGhiDoiChieu ban) =>
        new(TrailDoiChieuChung.So(ban.Payload, "freezeCount"),
            TrailDoiChieuChung.Chuoi(ban.Payload, "rServerCommitA"),
            TrailDoiChieuChung.Chuoi(ban.Payload, "rServerCommitB"),
            TrailDoiChieuChung.Chuoi(ban.Payload, "rServerCommitC"));
}
