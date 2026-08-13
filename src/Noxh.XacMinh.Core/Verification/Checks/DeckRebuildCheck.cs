using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Decks;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Hạng mục 8: <b>dựng lại</b> chồng phiếu từ hạt giống đã cam kết rồi so mã băm với bản niêm phong.
/// Khác hạng mục mã băm chồng phiếu (chỉ so bản công bố với bản niêm phong): ở đây công cụ tự sinh
/// ra chồng phiếu bằng phép xáo copy nguyên văn từ backend, nên nó chứng minh chồng phiếu <b>mọc ra
/// từ hạt giống</b> chứ không do ai sắp đặt. Vòng phân căn ưu tiên dựng lại cả <b>quỹ căn ưu tiên</b>
/// của từng loại trước khi dựng chồng phiếu.
/// </summary>
internal static class DeckRebuildCheck
{
    private const string DatGiaiThich =
        "Chồng phiếu này dựng lại được từ hạt giống đã cam kết trước lễ và ra đúng mã băm đã niêm phong: "
        + "thứ tự vé không do ai xếp mà mọc ra từ hạt giống. Muốn đẩy một lá vé trúng về tay ai đó thì "
        + "phải đổi hạt giống, mà hạt giống đã bị khoá bởi cam kết công bố trước.";

    private const string DatGiaiThichUuTien =
        "Quỹ căn ưu tiên của loại căn này và cả chồng phiếu của nó dựng lại được từ hạt giống đã cam kết "
        + "trước lễ, ra đúng mã băm đã niêm phong: chính việc căn nào được đưa vào quỹ ưu tiên cũng do hạt "
        + "giống quyết định, không do người sắp — cũng không ai chọn được ai nhận căn nào.";

    private const string KhongDatGiaiThich =
        "Chồng phiếu dựng lại từ hạt giống đã công bố ra mã băm KHÁC bản đã niêm phong: chồng phiếu đang "
        + "công bố không mọc ra từ hạt giống đó — hoặc hạt giống, hoặc chồng phiếu, đã bị thay.";

    private const string KhongDatGiaiThichUuTien = KhongDatGiaiThich
        + " Trước khi kết luận, xem lại danh mục căn đang dùng có đúng là danh mục của dự án này không: "
        + "dựng lại quỹ căn ưu tiên bằng một danh mục khác thì cũng ra mã băm khác.";

    public static IEnumerable<CheckResult> Run(VerificationInput input)
    {
        var dungDuoc = (input.Report.Decks ?? [])
            .Where(deck => deck is not null)
            .Select(deck => (Deck: deck, TaiLap: DeckRebuilder.Rebuild(input.Report, deck, input.Catalog)))
            .Where(x => x.TaiLap is not null)
            .ToList();

        if (dungDuoc.Count == 0)
        {
            yield return new CheckResult(
                CheckIds.DeckRebuild,
                "Tái lập chồng phiếu từ hạt giống",
                CheckStatus.KhongKiemDuoc,
                "Báo cáo không có chồng phiếu nào thuộc vòng công cụ dựng lại được (vòng quyền mua, vòng "
                + "phân căn ưu tiên), nên không dựng lại được chồng phiếu nào từ hạt giống để đối chiếu.")
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
        var title = taiLap.TypeCode is { } loai
            ? $"Tái lập vòng phân căn ưu tiên — loại căn {loai}"
            : $"Tái lập chồng phiếu vòng {taiLap.Round} từ hạt giống";
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
        var vongUuTien = taiLap.TypeCode is not null;

        return new CheckResult(
            id,
            title,
            khop ? CheckStatus.Dat : CheckStatus.KhongDat,
            (khop, vongUuTien) switch
            {
                (true, false) => DatGiaiThich,
                (true, true) => DatGiaiThichUuTien,
                (false, false) => KhongDatGiaiThich,
                (false, true) => KhongDatGiaiThichUuTien,
            },
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
    /// dẫn xuất, và thành phần chồng phiếu kèm nguồn của thành phần đó. Vòng phân căn ưu tiên kèm
    /// thêm quỹ căn ưu tiên đã dựng lại — thứ mà không báo cáo nào công bố sẵn.
    /// </summary>
    private static IReadOnlyList<CheckMetric> SoLieu(DeckRebuild taiLap)
    {
        var soLieu = new List<CheckMetric>
        {
            new("Vòng", taiLap.Round),
            new("Nhãn dẫn xuất hạt giống", taiLap.SeedLabel),
            new($"Hạt giống gốc đã dùng (MASTER_SEED vòng {taiLap.EntropyRound})", Co(taiLap.MasterSeed)),
            new("Hạt giống dẫn xuất của chồng phiếu", Co(taiLap.RoundSeed)),
            new("Quy mô dùng để dựng lại", taiLap.Size?.ToString() ?? KhongCo),
            new("Số vé trúng dùng để dựng lại", taiLap.WonCount?.ToString() ?? KhongCo),
            new("Nguồn thành phần chồng phiếu", Co(taiLap.CompositionSource)),
        };

        if (taiLap.TypeCode is not { } loai) return soLieu;

        soLieu.Add(new CheckMetric("Loại căn", loai));
        soLieu.Add(new CheckMetric("Nhãn dẫn xuất quỹ căn ưu tiên", Co(taiLap.PoolSeedLabel)));
        soLieu.Add(new CheckMetric("Hạt giống dẫn xuất quỹ căn ưu tiên", Co(taiLap.PoolSeed)));
        soLieu.Add(new CheckMetric(
            $"Số căn loại {loai} trong danh mục đang dùng",
            taiLap.CatalogUnitCount?.ToString() ?? KhongCo));
        soLieu.Add(new CheckMetric(
            "Quỹ căn ưu tiên dựng lại từ hạt giống (theo thứ tự)",
            taiLap.PriorityUnits is { Count: > 0 } quy ? string.Join(" · ", quy) : KhongCo));

        return soLieu;
    }

    private const string KhongCo = "(không công bố)";

    private static string Co(string? giaTri) => string.IsNullOrWhiteSpace(giaTri) ? KhongCo : giaTri;
}
