using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Hạng mục 2: <c>masterSeed == SHA-256(rServer ‖ rSupervisor ‖ blockHash)</c> — hạt giống gốc của
/// vòng đúng là tổ hợp của ba nguồn đã công bố hay không, từng vòng một.
/// </summary>
internal static class MasterSeedCheck
{
    private const string DatGiaiThich =
        "Hạt giống gốc của vòng đúng bằng mã băm của ba nguồn đã công bố — phần ngẫu nhiên máy chủ, phần "
        + "ngẫu nhiên tổ giám sát, và mã băm khối neo: không bên nào một mình chọn được hạt giống.";

    private const string KhongDatGiaiThich =
        "Hạt giống gốc đang công bố KHÁC mã băm của ba nguồn đã công bố: hạt giống không mọc ra từ ba nguồn "
        + "đó, nên mọi kết quả bốc từ nó không còn được ba nguồn ràng buộc.";

    public static IEnumerable<CheckResult> Run(VerificationInput input) =>
        NguonNgauNhienChung.TungVong(
            input,
            CheckIds.MasterSeed,
            "Hạt giống gốc",
            "Báo cáo không công bố nguồn ngẫu nhiên vòng nào, nên không kiểm được hạt giống gốc có đúng là "
            + "tổ hợp của ba nguồn đã công bố hay không.",
            Kiem);

    private static CheckResult Kiem(EntropySource nguon, string id, string ten)
    {
        var title = $"Hạt giống gốc vòng {ten}";
        var soLieu = SoLieu(nguon);

        CheckResult ChuaKiemDuoc(string vi) =>
            new(id, title, CheckStatus.KhongKiemDuoc, vi, Expected: Hex.ChuanHoa(nguon.MasterSeed))
            {
                Metrics = soLieu,
            };

        if (Hex.Doc(nguon.MasterSeed) is null)
            return ChuaKiemDuoc(string.IsNullOrWhiteSpace(nguon.MasterSeed)
                ? "Vòng này chưa công bố hạt giống gốc (cổng chưa đóng), nên chưa có gì để đối chiếu."
                : "Hạt giống gốc của vòng này không phải chuỗi hợp lệ, nên không đối chiếu được.");

        var rServer = Hex.Doc(nguon.RServer);
        var rSupervisor = Hex.Doc(nguon.RSupervisor);
        var blockHash = Hex.Doc(nguon.BlockHash);

        var thieu = new List<string>();
        if (rServer is null) thieu.Add("phần ngẫu nhiên máy chủ");
        if (rSupervisor is null) thieu.Add("phần ngẫu nhiên tổ giám sát");
        if (blockHash is null) thieu.Add("mã băm khối neo");

        if (thieu.Count > 0)
            return ChuaKiemDuoc(
                $"Vòng này chưa công bố đủ ba nguồn của hạt giống (thiếu {string.Join(", ", thieu)}), "
                + "nên chưa dựng lại được hạt giống để đối chiếu.");

        var tinhDuoc = Convert.ToHexString(MasterSeed.Build(rServer!, rSupervisor!, blockHash!)).ToLowerInvariant();
        var khop = string.Equals(tinhDuoc, Hex.ChuanHoa(nguon.MasterSeed), StringComparison.Ordinal);

        return new CheckResult(
            id,
            title,
            khop ? CheckStatus.Dat : CheckStatus.KhongDat,
            khop ? DatGiaiThich : KhongDatGiaiThich,
            Expected: Hex.ChuanHoa(nguon.MasterSeed),
            Actual: tinhDuoc,
            // Ba nguồn nối byte thô, không dấu phân cách — hiện dạng hex để người kiểm tự băm lại
            // bằng công cụ khác (`xxd -r -p | sha256sum`).
            Preimage: Hex.ChuanHoa(nguon.RServer) + Hex.ChuanHoa(nguon.RSupervisor) + Hex.ChuanHoa(nguon.BlockHash))
        {
            Metrics = soLieu,
        };
    }

    /// <summary>Ba nguồn đầu vào của hạt giống, hiện đủ ở chế độ chuyên sâu — kèm định danh khối neo.</summary>
    private static IReadOnlyList<CheckMetric> SoLieu(EntropySource nguon) =>
    [
        new CheckMetric("Vòng", NguonNgauNhienChung.Co(nguon.Round)),
        new CheckMetric("Ngẫu nhiên máy chủ (R_server)", NguonNgauNhienChung.Co(Hex.ChuanHoa(nguon.RServer))),
        new CheckMetric("Ngẫu nhiên tổ giám sát (R_supervisor)", NguonNgauNhienChung.Co(Hex.ChuanHoa(nguon.RSupervisor))),
        new CheckMetric("Mã băm khối neo (H_blockchain)", NguonNgauNhienChung.Co(Hex.ChuanHoa(nguon.BlockHash))),
        new CheckMetric("Chuỗi khối neo", NguonNgauNhienChung.Co(nguon.AnchorChain)),
        new CheckMetric("Độ cao khối neo", nguon.BlockHeight?.ToString() ?? NguonNgauNhienChung.KhongCo),
    ];
}
