using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Hạng mục 3: <c>deckHash == SHA-256(canonical(tickets))</c> — nội dung vé công bố có đúng bản đã
/// niêm phong trước khi mở hay không, từng chồng phiếu một.
/// </summary>
internal static class DeckHashCheck
{
    private const string DatGiaiThich =
        "Nội dung vé công bố khớp mã băm đã niêm phong trước khi mở chồng phiếu: sau khi niêm phong, "
        + "không ai đổi được vé nào. (Chưa chứng minh chồng phiếu mọc ra từ hạt giống — đó là hạng mục tái lập riêng.)";

    private const string KhongDatGiaiThich =
        "Mã băm tính từ nội dung vé đang công bố KHÁC mã băm đã niêm phong: nội dung chồng phiếu đã bị đổi "
        + "sau khi niêm phong, hoặc bản công bố không phải bản đã niêm phong.";

    public static IEnumerable<CheckResult> Run(VerificationInput input)
    {
        var decks = input.Report.Decks;

        if (decks is null || decks.Count == 0)
        {
            yield return new CheckResult(
                CheckIds.DeckHash,
                "Mã băm chồng phiếu",
                CheckStatus.KhongKiemDuoc,
                "Báo cáo không có chồng phiếu nào, nên không kiểm được nội dung vé có đúng bản đã niêm phong không.");
            yield break;
        }

        for (var i = 0; i < decks.Count; i++)
            yield return Kiem(decks[i], i);
    }

    private static CheckResult Kiem(Deck deck, int index)
    {
        var ten = string.IsNullOrWhiteSpace(deck.Round) ? $"#{index + 1}" : deck.Round!;
        var id = $"{CheckIds.DeckHash}:{ten}";
        var title = $"Mã băm chồng phiếu {ten}";

        CheckResult ChuaKiemDuoc(string vi) =>
            new(id, title, CheckStatus.KhongKiemDuoc, vi, Expected: deck.DeckHash);

        if (string.IsNullOrWhiteSpace(deck.DeckHash))
            return ChuaKiemDuoc("Chồng phiếu này không công bố mã băm đã niêm phong, nên không có gì để đối chiếu.");

        if (deck.Tickets is null)
            return ChuaKiemDuoc("Chồng phiếu này chưa công bố nội dung vé, nên chưa kiểm được nó có đúng bản đã niêm phong không.");

        if (deck.Tickets.Any(t => t is null))
            return ChuaKiemDuoc("Chồng phiếu này có ô vé rỗng trong bản công bố, nên chuỗi nội dung để băm lại không đầy đủ.");

        var tickets = deck.Tickets.Cast<string>().ToList();
        var tinhDuoc = CanonicalDeckSerializer.Hash(tickets);
        var khop = string.Equals(tinhDuoc, deck.DeckHash!.Trim(), StringComparison.OrdinalIgnoreCase);

        return new CheckResult(
            id,
            title,
            khop ? CheckStatus.Dat : CheckStatus.KhongDat,
            khop ? DatGiaiThich : KhongDatGiaiThich,
            Expected: deck.DeckHash!.Trim().ToLowerInvariant(),
            Actual: tinhDuoc,
            Preimage: CanonicalDeckSerializer.CanonicalText(tickets));
    }
}
