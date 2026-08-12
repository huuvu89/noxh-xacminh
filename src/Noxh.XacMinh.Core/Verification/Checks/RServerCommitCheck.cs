using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Hạng mục 1: <c>SHA-256(rServer) == rServerCommit</c> — máy chủ đã niêm phong phần ngẫu nhiên của
/// mình <b>trước</b> khi biết mốc neo hay chưa, từng vòng một.
/// </summary>
internal static class RServerCommitCheck
{
    private const string DatGiaiThich =
        "Phần ngẫu nhiên máy chủ công bố sau lễ đúng là phần đã niêm phong từ trước: máy chủ không thể "
        + "thử đi thử lại phần của mình sau khi đã thấy mốc neo, vì cam kết công bố sớm ràng nó lại.";

    private const string KhongDatGiaiThich =
        "Mã băm của phần ngẫu nhiên máy chủ đang công bố KHÁC cam kết đã công bố trước lễ: phần ngẫu nhiên "
        + "này không phải phần đã niêm phong, tức máy chủ đã đổi phần của mình sau khi niêm phong.";

    public static IEnumerable<CheckResult> Run(VerificationInput input) =>
        NguonNgauNhienChung.TungVong(
            input,
            CheckIds.RServerCommit,
            "Cam kết ngẫu nhiên máy chủ",
            "Báo cáo không công bố nguồn ngẫu nhiên vòng nào, nên không kiểm được máy chủ có niêm phong "
            + "phần ngẫu nhiên của mình trước khi biết mốc neo hay không.",
            Kiem);

    private static CheckResult Kiem(EntropySource nguon, string id, string ten)
    {
        var title = $"Cam kết ngẫu nhiên máy chủ vòng {ten}";
        var soLieu = SoLieu(nguon);

        CheckResult ChuaKiemDuoc(string vi) =>
            new(id, title, CheckStatus.KhongKiemDuoc, vi, Expected: Hex.ChuanHoa(nguon.RServerCommit))
            {
                Metrics = soLieu,
            };

        var camKet = Hex.Doc(nguon.RServerCommit);
        if (camKet is null)
            return ChuaKiemDuoc(string.IsNullOrWhiteSpace(nguon.RServerCommit)
                ? "Vòng này chưa công bố cam kết ngẫu nhiên máy chủ, nên không có gì để đối chiếu."
                : "Cam kết ngẫu nhiên máy chủ của vòng này không phải chuỗi mã băm hợp lệ, nên không đối chiếu được.");

        var rServer = Hex.Doc(nguon.RServer);
        if (rServer is null)
            return ChuaKiemDuoc(string.IsNullOrWhiteSpace(nguon.RServer)
                ? "Vòng này chưa mở phần ngẫu nhiên máy chủ (cổng chưa đóng), nên chưa kiểm được nó có khớp cam kết không."
                : "Phần ngẫu nhiên máy chủ của vòng này không phải chuỗi hợp lệ, nên chưa băm lại được để đối chiếu.");

        var tinhDuoc = Hex.Sha256Hex(rServer);
        var khop = string.Equals(tinhDuoc, Hex.ChuanHoa(nguon.RServerCommit), StringComparison.Ordinal);

        return new CheckResult(
            id,
            title,
            khop ? CheckStatus.Dat : CheckStatus.KhongDat,
            khop ? DatGiaiThich : KhongDatGiaiThich,
            Expected: Hex.ChuanHoa(nguon.RServerCommit),
            Actual: tinhDuoc,
            Preimage: Hex.ChuanHoa(nguon.RServer))
        {
            Metrics = soLieu,
        };
    }

    private static IReadOnlyList<CheckMetric> SoLieu(EntropySource nguon) =>
    [
        new CheckMetric("Vòng", NguonNgauNhienChung.Co(nguon.Round)),
        new CheckMetric("Ngẫu nhiên máy chủ (R_server)", NguonNgauNhienChung.Co(Hex.ChuanHoa(nguon.RServer))),
    ];
}
