using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Decks;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Hạng mục 8: <b>dựng lại</b> chồng phiếu vòng quyền mua từ hạt giống đã cam kết rồi so mã băm với
/// bản niêm phong. Khác hạng mục mã băm chồng phiếu (chỉ so bản công bố với bản niêm phong): ở đây
/// công cụ tự sinh ra chồng phiếu bằng phép xáo copy nguyên văn từ backend, nên nó chứng minh chồng
/// phiếu <b>mọc ra từ hạt giống</b> chứ không do ai sắp đặt.
/// </summary>
internal static class DeckRebuildCheck
{
    private const string DatGiaiThich =
        "Chồng phiếu này dựng lại được từ hạt giống đã cam kết trước lễ và ra đúng mã băm đã niêm phong: "
        + "thứ tự vé không do ai xếp mà mọc ra từ hạt giống. Muốn đẩy một lá vé trúng về tay ai đó thì "
        + "phải đổi hạt giống, mà hạt giống đã bị khoá bởi cam kết công bố trước.";

    private const string KhongDatGiaiThich =
        "Chồng phiếu dựng lại từ hạt giống đã công bố ra mã băm KHÁC bản đã niêm phong: chồng phiếu đang "
        + "công bố không mọc ra từ hạt giống đó — hoặc hạt giống, hoặc chồng phiếu, đã bị thay.";

    public static IEnumerable<CheckResult> Run(VerificationInput input)
    {
        var dungDuoc = (input.Report.Decks ?? [])
            .Select(deck => (Deck: deck, TaiLap: DeckRebuilder.Rebuild(input.Report, deck)))
            .Where(x => x.TaiLap is not null)
            .ToList();

        if (dungDuoc.Count == 0)
        {
            yield return new CheckResult(
                CheckIds.DeckRebuild,
                "Tái lập chồng phiếu vòng quyền mua",
                CheckStatus.KhongKiemDuoc,
                "Báo cáo không có chồng phiếu vòng quyền mua, nên không dựng lại được chồng phiếu nào từ "
                + "hạt giống để đối chiếu.")
            {
                Metrics = [new CheckMetric("Nhãn dẫn xuất hạt giống", LotteryLabels.A1Deck)],
            };
            yield break;
        }

        foreach (var (deck, taiLap) in dungDuoc)
            yield return Kiem(deck, taiLap!);
    }

    private static CheckResult Kiem(Deck deck, DeckRebuild taiLap)
    {
        var id = $"{CheckIds.DeckRebuild}:{taiLap.Round}";
        var title = $"Tái lập chồng phiếu vòng {taiLap.Round} từ hạt giống";
        var soLieu = SoLieu(taiLap);

        CheckResult ChuaKiemDuoc(string vi) =>
            new(id, title, CheckStatus.KhongKiemDuoc, vi, Expected: Hex.ChuanHoa(deck.DeckHash)) { Metrics = soLieu };

        if (taiLap.Blocker is { } vuong)
            return ChuaKiemDuoc(vuong);

        if (string.IsNullOrWhiteSpace(deck.DeckHash))
            return ChuaKiemDuoc(
                "Chồng phiếu này không công bố mã băm đã niêm phong, nên bản dựng lại không có gì để đối "
                + "chiếu — lấy chính bản dựng lại làm chuẩn thì hạng mục này tự khớp với nó.");

        var khop = string.Equals(taiLap.DeckHash, Hex.ChuanHoa(deck.DeckHash), StringComparison.Ordinal);

        return new CheckResult(
            id,
            title,
            khop ? CheckStatus.Dat : CheckStatus.KhongDat,
            khop ? DatGiaiThich : KhongDatGiaiThich,
            Expected: Hex.ChuanHoa(deck.DeckHash),
            Actual: taiLap.DeckHash,
            // Preimage của bản DỰNG LẠI (không phải bản công bố): người kiểm băm lại chuỗi này bằng
            // công cụ khác là ra đúng "giá trị tính được".
            Preimage: CanonicalDeckSerializer.CanonicalText(taiLap.Tickets!))
        {
            Metrics = soLieu,
        };
    }

    /// <summary>
    /// Số liệu thô đủ để người kiểm toán tự chạy lại phép dựng bằng công cụ khác: hai hạt giống, nhãn
    /// dẫn xuất, và thành phần chồng phiếu kèm nguồn của thành phần đó.
    /// </summary>
    private static IReadOnlyList<CheckMetric> SoLieu(DeckRebuild taiLap) =>
    [
        new CheckMetric("Vòng", taiLap.Round),
        new CheckMetric("Nhãn dẫn xuất hạt giống", taiLap.SeedLabel),
        new CheckMetric($"Hạt giống gốc đã dùng (MASTER_SEED vòng {taiLap.EntropyRound})", Co(taiLap.MasterSeed)),
        new CheckMetric("Hạt giống dẫn xuất của chồng phiếu", Co(taiLap.RoundSeed)),
        new CheckMetric("Quy mô dùng để dựng lại", taiLap.Size?.ToString() ?? KhongCo),
        new CheckMetric("Số vé trúng dùng để dựng lại", taiLap.WonCount?.ToString() ?? KhongCo),
        new CheckMetric("Nguồn thành phần chồng phiếu", Co(taiLap.CompositionSource)),
    ];

    private const string KhongCo = "(không công bố)";

    private static string Co(string? giaTri) => string.IsNullOrWhiteSpace(giaTri) ? KhongCo : giaTri;
}
