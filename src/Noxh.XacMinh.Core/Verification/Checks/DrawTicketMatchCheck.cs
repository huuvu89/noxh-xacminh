using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Hạng mục 5: nội dung vé của từng lượt bốc phải là lá vé nằm ở <b>đúng vị trí đó</b> trong chồng
/// phiếu đã niêm phong. Hai hạng mục trước không bắt được kiểu tráo này: mã băm chồng phiếu chỉ nói
/// chồng phiếu không đổi, chuỗi băm nhật ký chỉ nói nhật ký không đổi — một nhật ký gán cho khách lá
/// vé của ô khác vẫn tự nhất quán và vẫn ĐẠT cả hai.
///
/// Đặc thù phải tôn trọng: vị trí vé cấp bằng sequence <b>không transactional</b> nên một lượt rút bị
/// rollback vẫn ĐỐT một vị trí, và lượt sau được thu hồi tại chỗ (nhận ô trống nhỏ nhất của deck).
/// Hệ quả: vị trí trong nhật ký KHÔNG đơn điệu theo thời gian và KHÔNG liên tục ⇒ chỉ được đối chiếu
/// theo <b>giá trị</b> vị trí, không suy diễn gì từ thứ tự dòng trong file, và ô phiếu không có lượt
/// bốc không phải dấu hiệu gian lận.
///
/// Kết luận ra theo từng vòng và liệt kê ĐỦ điểm lệch — dừng ở điểm lệch đầu tiên thì người kiểm
/// tưởng chỉ có một chỗ bị sửa.
/// </summary>
internal static class DrawTicketMatchCheck
{
    private const string Ten = "Vé từng lượt bốc";

    private const string DatGiaiThich =
        "Mỗi lượt bốc của vòng này nhận đúng lá vé nằm ở vị trí đó trong chồng phiếu đã niêm phong: sau khi "
        + "niêm phong, không ai tráo kết quả của ai. (Ô phiếu không có lượt bốc là chuyện bình thường — vị trí "
        + "bị đốt khi một lượt rút bị huỷ giữa đường — nên số ô trống không nói lên điều gì về gian lận.)";

    public static IEnumerable<CheckResult> Run(VerificationInput input)
    {
        var nhatKy = input.Report.DrawLog;

        if (nhatKy is null || nhatKy.Count == 0)
        {
            yield return new CheckResult(
                CheckIds.DrawTicketMatch,
                Ten,
                CheckStatus.KhongKiemDuoc,
                "Báo cáo không công bố nhật ký bốc, nên không đối chiếu được lượt bốc nào với vé trong chồng phiếu.")
            {
                Metrics = [new CheckMetric("Số lượt bốc trong nhật ký", "0")],
            };
            yield break;
        }

        var chongPhieu = TheoVong(input.Report.Decks);

        // Thứ tự vòng cố định (A1 → A2 → B → C) để cùng một file luôn ra cùng một báo cáo, bất kể
        // thứ tự dòng người dùng thả vào.
        foreach (var vong in nhatKy
                     .GroupBy(e => e.Round?.Trim() ?? string.Empty, StringComparer.Ordinal)
                     .OrderBy(g => DrawLogHashChain.ThuTuVong(g.Key))
                     .ThenBy(g => g.Key, StringComparer.Ordinal))
            yield return Kiem(vong.Key, vong.ToList(), chongPhieu);
    }

    /// <summary>
    /// Ghép nhật ký ↔ chồng phiếu theo <b>tên vòng</b>: khối <c>decks</c> không bắt buộc công bố mã
    /// chồng phiếu. Vòng trống tên bị loại khỏi bảng — ghép hai bên bằng một cái tên rỗng là ghép bừa.
    /// </summary>
    private static Dictionary<string, List<Deck>> TheoVong(IReadOnlyList<Deck>? decks) =>
        (decks ?? [])
        .Where(d => !string.IsNullOrWhiteSpace(d.Round))
        .GroupBy(d => d.Round!.Trim(), StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

    private static CheckResult Kiem(string round, List<DrawLogEntry> luot, Dictionary<string, List<Deck>> chongPhieu)
    {
        var ten = string.IsNullOrWhiteSpace(round) ? "(không công bố)" : round;
        var id = $"{CheckIds.DrawTicketMatch}:{ten}";
        var title = $"{Ten} vòng {ten}";

        CheckResult ChuaKiemDuoc(string vi) =>
            new(id, title, CheckStatus.KhongKiemDuoc, vi)
            {
                Metrics = [new CheckMetric("Vòng", ten), new CheckMetric("Số lượt bốc trong vòng", luot.Count.ToString())],
            };

        if (string.IsNullOrWhiteSpace(round))
            return ChuaKiemDuoc(
                $"Có {luot.Count} lượt bốc không công bố thuộc vòng nào, nên không biết phải đối chiếu chúng với "
                + "chồng phiếu nào.");

        if (!chongPhieu.TryGetValue(round, out var cung))
            return ChuaKiemDuoc(
                $"Vòng {ten} có {luot.Count} lượt bốc nhưng báo cáo không công bố chồng phiếu của vòng này, nên "
                + "không đối chiếu được lượt bốc nào với vé đã niêm phong.");

        if (cung.Count > 1)
            return ChuaKiemDuoc(
                $"Báo cáo công bố {cung.Count} chồng phiếu cùng mang tên vòng {ten}, nên không xác định được "
                + "lượt bốc của vòng này thuộc chồng phiếu nào để đối chiếu.");

        var deck = cung[0];

        if (deck.Tickets is null)
            return ChuaKiemDuoc(
                $"Chồng phiếu vòng {ten} chưa công bố nội dung vé, nên chưa đối chiếu được lượt bốc nào với vé ở "
                + "vị trí tương ứng.");

        if (deck.Tickets.Any(t => t is null))
            return ChuaKiemDuoc(
                $"Chồng phiếu vòng {ten} có ô vé rỗng trong bản công bố, nên nội dung vé ở những ô đó không có gì "
                + "để đối chiếu.");

        // Cả hai bên đều khai mã chồng phiếu mà lệch nhau ⇒ phép ghép theo tên vòng mất căn cứ: đối
        // chiếu tiếp là đối chiếu với một chồng phiếu khác rồi kết luận thay cho chồng phiếu này.
        if (LechMaChongPhieu(deck, luot) is { } lechMa)
            return ChuaKiemDuoc(
                $"Chồng phiếu vòng {ten} mang mã {lechMa.Deck}, còn lượt bốc của vòng này khai bốc từ chồng phiếu "
                + $"{lechMa.NhatKy}, nên không rõ nhật ký và chồng phiếu đang công bố có phải của cùng một lần "
                + "niêm phong.");

        return DoiChieu(id, title, ten, deck.Tickets!, luot);
    }

    private static (string Deck, string NhatKy)? LechMaChongPhieu(Deck deck, List<DrawLogEntry> luot)
    {
        if (!Guid.TryParse(deck.DeckId?.Trim(), out var maDeck)) return null;

        var lech = luot.FirstOrDefault(e =>
            Guid.TryParse(e.DeckId?.Trim(), out var ma) && ma != maDeck);

        return lech is null ? null : (maDeck.ToString(), lech.DeckId!.Trim());
    }

    /// <summary>Một điểm lệch đã đọc thành câu tiếng Việt, kèm cặp giá trị cho chế độ chuyên sâu.</summary>
    private sealed record Lech(string MoTa, string KyVong, string ThucTe);

    private static CheckResult DoiChieu(
        string id,
        string title,
        string ten,
        IReadOnlyList<string?> ve,
        List<DrawLogEntry> luot)
    {
        var soO = ve.Count;
        var phamVi = soO == 0 ? "chồng phiếu rỗng" : $"0–{soO - 1}";

        var soLuotTheoViTri = luot
            .Where(e => e.Position is not null)
            .GroupBy(e => e.Position!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var lech = new List<Lech>();
        var thieu = new List<string>();

        // Duyệt theo giá trị vị trí (thiếu vị trí thì xếp cuối) để danh sách điểm lệch không đổi theo
        // thứ tự dòng trong file.
        foreach (var e in luot
                     .OrderBy(e => e.Position ?? int.MaxValue)
                     .ThenBy(e => e.Payload, StringComparer.Ordinal)
                     .ThenBy(e => e.ApplicantId, StringComparer.Ordinal))
        {
            if (e.Position is not { } viTri)
            {
                thieu.Add("có lượt bốc không công bố vị trí phiếu, nên không biết phải đối chiếu với ô phiếu nào");
                continue;
            }

            if (e.Payload is null)
            {
                thieu.Add($"lượt bốc ở vị trí {viTri} không công bố nội dung vé, nên không có gì để đối chiếu");
                continue;
            }

            if (viTri < 0 || viTri >= soO)
            {
                lech.Add(new Lech(
                    $"vị trí {viTri}: nằm ngoài phạm vi hợp lệ {phamVi} của chồng phiếu ({soO} ô phiếu)",
                    $"vị trí trong phạm vi {phamVi}",
                    $"vị trí {viTri}"));
                continue;
            }

            if (!string.Equals(e.Payload, ve[viTri], StringComparison.Ordinal))
                lech.Add(new Lech(
                    $"vị trí {viTri}: nhật ký công bố vé «{Gon(e.Payload)}», chồng phiếu niêm phong vé «{Gon(ve[viTri]!)}»",
                    ve[viTri]!,
                    e.Payload));
        }

        // Ô phiếu chỉ phát cho một lượt (khoá duy nhất phía cơ sở dữ liệu); file người dùng thả vào
        // không có ràng buộc đó, nên trùng ô là dấu hiệu có lượt bốc bị chèn.
        foreach (var (viTri, so) in soLuotTheoViTri.Where(p => p.Value > 1).OrderBy(p => p.Key))
            lech.Add(new Lech(
                $"vị trí {viTri}: có {so} lượt bốc cùng nhận một ô phiếu, mà mỗi ô phiếu chỉ được phát cho một lượt",
                "1 lượt bốc trên mỗi ô phiếu",
                $"{so} lượt bốc ở vị trí {viTri}"));

        var soOCoLuot = soLuotTheoViTri.Keys.Count(vt => vt >= 0 && vt < soO);

        var soLieu = new List<CheckMetric>
        {
            new("Vòng", ten),
            new("Số ô phiếu trong chồng phiếu", soO.ToString()),
            new("Số lượt bốc trong vòng", luot.Count.ToString()),
            new("Số ô phiếu không có lượt bốc", (soO - soOCoLuot).ToString()),
        };

        if (lech.Count > 0) return KhongDat(id, title, ten, lech, soLieu);

        if (thieu.Count > 0)
        {
            soLieu.Add(new CheckMetric("Số lượt bốc chưa đủ dữ liệu để đối chiếu", thieu.Count.ToString()));

            return new CheckResult(
                id,
                title,
                CheckStatus.KhongKiemDuoc,
                $"Vòng {ten} còn {thieu.Count} lượt bốc chưa đối chiếu được: {thieu[0]}. Những lượt còn lại đều "
                + "nhận đúng vé ở vị trí của mình, nhưng chừng nào còn chỗ thiếu thì chưa kết luận được cả vòng.")
            {
                Metrics = soLieu,
            };
        }

        return Dat(id, title, ve, luot, soLieu);
    }

    private static CheckResult Dat(
        string id,
        string title,
        IReadOnlyList<string?> ve,
        List<DrawLogEntry> luot,
        List<CheckMetric> soLieu)
    {
        // Lượt ở ô nhỏ nhất làm ví dụ đối chiếu tay được: chế độ chuyên sâu luôn có một cặp giá trị.
        var mau = luot.OrderBy(e => e.Position!.Value).First();

        soLieu.Add(new CheckMetric("Lượt bốc lấy làm ví dụ đối chiếu", $"vị trí {mau.Position!.Value}"));

        return new CheckResult(
            id,
            title,
            CheckStatus.Dat,
            DatGiaiThich,
            Expected: ve[mau.Position!.Value],
            Actual: mau.Payload)
        {
            Metrics = soLieu,
        };
    }

    private static CheckResult KhongDat(
        string id,
        string title,
        string ten,
        List<Lech> lech,
        List<CheckMetric> soLieu)
    {
        soLieu.Add(new CheckMetric("Số điểm lệch", lech.Count.ToString()));
        soLieu.AddRange(lech.Select((l, i) => new CheckMetric($"Điểm lệch {i + 1}", l.MoTa)));

        return new CheckResult(
            id,
            title,
            CheckStatus.KhongDat,
            $"Vòng {ten} có {lech.Count} điểm lệch giữa nhật ký bốc và chồng phiếu đã niêm phong — điểm lệch đầu "
            + $"tiên: {lech[0].MoTa}. Nghĩa là có lượt bốc KHÔNG nhận lá vé nằm ở vị trí đó trong chồng phiếu: "
            + "kết quả đã bị tráo sau khi niêm phong, hoặc nhật ký đang công bố không phải nhật ký của chồng "
            + $"phiếu này. Cả {lech.Count} điểm lệch được liệt kê trong số liệu thô.",
            Expected: lech[0].KyVong,
            Actual: lech[0].ThucTe)
        {
            Metrics = soLieu,
        };
    }

    /// <summary>Vé trong câu giải thích phải đọc được; file bị sửa có thể nhồi payload dài bất kỳ.</summary>
    private static string Gon(string giaTri) =>
        giaTri.Length <= 60 ? giaTri : string.Concat(giaTri.AsSpan(0, 60), "…");
}
