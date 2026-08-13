using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Units;

namespace Noxh.XacMinh.Core.Decks;

/// <summary>
/// Dựng lưới ô phiếu từ báo cáo minh bạch: thuần, không I/O, tất định — vỏ giao diện chỉ vẽ lại.
/// Đây là phép <b>trình bày</b> dữ liệu đã công bố, không phải một hạng mục kiểm: nó không kết luận
/// ĐẠT/KHÔNG ĐẠT gì cả, việc đối chiếu nhật ký với chồng phiếu vẫn là của
/// <c>DrawTicketMatchCheck</c>.
/// </summary>
public static class DeckGridBuilder
{
    public static IReadOnlyList<DeckGrid> Build(TransparencyReport report, UnitCatalog? catalog = null) =>
        (report.Decks ?? [])
            .Select(deck => Dung(deck, NhatKyGhepDuoc(report, deck), DeckRebuilder.Rebuild(report, deck, catalog)))
            .ToList();

    /// <summary>
    /// Ghép nhật ký ↔ chồng phiếu theo <b>tên vòng</b>, đúng quy tắc của hạng mục kiểm vé từng lượt
    /// bốc: khối <c>decks</c> không bắt buộc công bố mã chồng phiếu. Vòng trống tên, hoặc hai chồng
    /// phiếu cùng tên vòng, thì không ghép — ghép bừa rồi tô màu lên lưới là dựng chuyện.
    /// </summary>
    private static List<DrawLogEntry>? NhatKyGhepDuoc(TransparencyReport report, Deck deck)
    {
        var vong = deck.Round?.Trim();
        if (string.IsNullOrEmpty(vong)) return null;

        var trung = report.Decks!.Count(d => string.Equals(d.Round?.Trim(), vong, StringComparison.Ordinal));
        if (trung != 1) return null;

        if (report.DrawLog is null) return null;

        return report.DrawLog
            .Where(e => string.Equals(e.Round?.Trim(), vong, StringComparison.Ordinal))
            .ToList();
    }

    private static DeckGrid Dung(Deck deck, List<DrawLogEntry>? nhatKy, DeckRebuild? taiLap)
    {
        // Số ô lấy theo nội dung vé đã công bố, không theo trường size: size là con số deck tự khai,
        // còn thứ deckHash niêm phong là mảng tickets. Chưa mở vé thì chưa có lưới để vẽ.
        var ve = deck.Tickets ?? [];

        var theoViTri = (nhatKy ?? [])
            .Where(e => e.Position is >= 0 && e.Position < ve.Count)
            .GroupBy(e => e.Position!.Value)
            .ToDictionary(g => g.Key, g => g.Select(e => new CellDraw(e.ApplicantId, e.AutoDrawn)).ToList());

        // Bản dựng lại ngắn hơn bản công bố thì những ô thừa là LỆCH, không phải "chưa biết": bản
        // dựng lại đã có, chỉ là ở vị trí đó nó không sinh ra lá vé nào.
        var banDungLai = taiLap?.Tickets;

        var o = ve.Select((payload, viTri) =>
        {
            var (kind, label) = TicketPayload.Doc(payload);

            return new DeckCell(
                viTri,
                payload,
                kind,
                label,
                theoViTri.TryGetValue(viTri, out var boc) ? boc : [],
                banDungLai is null ? null : viTri < banDungLai.Count && banDungLai[viTri] == payload);
        }).ToList();

        return new DeckGrid(deck.Round, deck.DeckHash, o, TomTat(o, nhatKy, theoViTri.Count, banDungLai), taiLap);
    }

    private static DeckSummary TomTat(
        List<DeckCell> o,
        List<DrawLogEntry>? nhatKy,
        int soODaBoc,
        IReadOnlyList<string>? banDungLai)
    {
        var trung = o.Count(x => x.Kind == TicketKind.Trung);
        var khopTaiLap = banDungLai is null ? (int?)null : o.Count(x => x.MatchesRebuild == true);

        if (nhatKy is null) return new DeckSummary(o.Count, trung, null, null, null, khopTaiLap);

        // Đếm theo lượt bốc trong vòng, kể cả lượt khai vị trí ngoài phạm vi chồng phiếu: giấu nó đi
        // thì thanh tóm tắt lệch với nhật ký công bố. Lượt không công bố autoDrawn không thuộc bên nào.
        return new DeckSummary(
            o.Count,
            trung,
            nhatKy.Count(e => e.AutoDrawn == false),
            nhatKy.Count(e => e.AutoDrawn == true),
            o.Count - soODaBoc,
            khopTaiLap);
    }
}
