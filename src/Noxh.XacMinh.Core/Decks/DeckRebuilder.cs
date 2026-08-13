using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Decks;

/// <summary>
/// Kết quả dựng lại một chồng phiếu từ hạt giống đã công bố. <see cref="Tickets"/> có giá trị là
/// dựng được; <see cref="Blocker"/> có giá trị là <b>chưa dựng được vì thiếu/hỏng dữ liệu</b> —
/// không phải "không khớp". Hai ca đó phải ra hai kết luận khác nhau.
/// </summary>
public sealed record DeckRebuild(string Round, string SeedLabel, string EntropyRound)
{
    /// <summary>Hạt giống gốc của vòng đã dùng (hex), lấy từ khối nguồn ngẫu nhiên đã công bố.</summary>
    public string? MasterSeed { get; init; }

    /// <summary>Hạt giống dẫn xuất của chính chồng phiếu này (hex) = SHA-256(hạt giống gốc ‖ nhãn).</summary>
    public string? RoundSeed { get; init; }

    public int? Size { get; init; }

    public int? WonCount { get; init; }

    /// <summary>Thành phần chồng phiếu lấy từ đâu — số liệu niêm phong hay đếm lại từ vé đã mở.</summary>
    public string? CompositionSource { get; init; }

    public IReadOnlyList<string>? Tickets { get; init; }

    public string? DeckHash { get; init; }

    public string? Blocker { get; init; }
}

/// <summary>
/// Dựng lại chồng phiếu từ hạt giống đã cam kết, bằng phép xáo copy nguyên văn từ backend
/// (<see cref="SeededShuffle"/>). Thuần, không I/O, tất định.
///
/// Đây là bước nhảy so với hạng mục mã băm chồng phiếu: mã băm chỉ chứng minh nội dung vé không đổi
/// <b>sau khi</b> niêm phong; dựng lại chứng minh chính chồng phiếu đó mọc ra từ hạt giống đã cam
/// kết <b>trước</b> lễ, nên không ai sắp đặt được vị trí vé trúng.
///
/// Hiện chỉ dựng lại vòng quyền mua (A1) — vòng duy nhất mà thành phần chồng phiếu suy được hoàn
/// toàn từ báo cáo minh bạch. A2/B/C còn cần danh mục căn và thứ tự hồ sơ đầu vào; công cụ không
/// dựng lại được thì <b>không kết luận</b> chứ không đoán.
/// </summary>
public static class DeckRebuilder
{
    /// <summary>Hạt giống gốc của chồng phiếu A1 là hạt giống vòng A (gate A đóng sinh ra nó).</summary>
    private const string VongHatGiongA1 = "A";

    /// <summary>
    /// Trần quy mô chồng phiếu dựng lại. Dự án lớn nhất ~900 vé; con số trong file người dùng thả
    /// vào thì có thể bị sửa thành bất kỳ thứ gì — cấp phát theo nó là treo tab trình duyệt.
    /// </summary>
    private const int TranQuyMo = 100_000;

    /// <summary>Nhãn dẫn xuất hạt giống của chồng phiếu, <c>null</c> nếu công cụ chưa dựng lại được vòng đó.</summary>
    public static string? SeedLabel(string? round) =>
        Chuan(round) == LotteryLabels.RoundA1 ? LotteryLabels.A1Deck : null;

    /// <summary>Dựng lại một chồng phiếu; <c>null</c> nếu công cụ chưa biết dựng lại vòng của nó.</summary>
    public static DeckRebuild? Rebuild(TransparencyReport report, Deck deck)
    {
        var round = Chuan(deck.Round);
        if (SeedLabel(round) is not { } label) return null;

        var ketQua = new DeckRebuild(round!, label, VongHatGiongA1);

        if (HatGiongGoc(report, out var masterSeedHex, out var masterSeed) is { } thieuHatGiong)
            return ketQua with { MasterSeed = masterSeedHex, Blocker = thieuHatGiong };

        var roundSeed = MasterSeed.RoundSeed(masterSeed!, label);
        ketQua = ketQua with
        {
            MasterSeed = masterSeedHex,
            RoundSeed = Convert.ToHexString(roundSeed).ToLowerInvariant(),
        };

        var (quyMo, veTrung, nguon) = ThanhPhan(deck);
        if (quyMo is null) return ketQua with { Blocker = nguon };

        ketQua = ketQua with { Size = quyMo, WonCount = veTrung, CompositionSource = nguon };

        // Chồng phiếu A1 trước khi xáo: [TRÚNG QUYỀN MUA × w, KHÔNG TRÚNG ƯU TIÊN × (n − w)] —
        // đúng thứ tự A1DeckPlan.Compute dựng, vì thứ tự trước khi xáo cũng quyết định kết quả.
        var truocKhiXao = new List<string>(quyMo.Value);
        truocKhiXao.AddRange(Enumerable.Repeat(LotteryLabels.A1Win, veTrung!.Value));
        truocKhiXao.AddRange(Enumerable.Repeat(LotteryLabels.A1Lose, quyMo.Value - veTrung.Value));

        var tickets = SeededShuffle.Shuffle(truocKhiXao, roundSeed);

        return ketQua with { Tickets = tickets, DeckHash = CanonicalDeckSerializer.Hash(tickets) };
    }

    /// <summary>Bản dựng lại của mọi chồng phiếu công cụ biết dựng, giữ nguyên thứ tự báo cáo.</summary>
    public static IReadOnlyList<DeckRebuild> RebuildAll(TransparencyReport report) =>
        (report.Decks ?? []).Select(deck => Rebuild(report, deck)).OfType<DeckRebuild>().ToList();

    /// <summary>Trả về lý do KHÔNG lấy được hạt giống gốc, <c>null</c> nếu lấy được.</summary>
    private static string? HatGiongGoc(TransparencyReport report, out string? hex, out byte[]? bytes)
    {
        hex = null;
        bytes = null;

        var nguon = (report.EntropySources ?? [])
            .Where(n => Chuan(n.Round) == VongHatGiongA1)
            .ToList();

        if (nguon.Count == 0)
            return $"Báo cáo không công bố nguồn ngẫu nhiên vòng {VongHatGiongA1}, nên không có hạt giống "
                + "nào để dựng lại chồng phiếu vòng quyền mua.";

        if (nguon.Count > 1)
            return $"Báo cáo công bố nhiều khối nguồn ngẫu nhiên cùng mang tên vòng {VongHatGiongA1}, nên "
                + "không biết lấy hạt giống nào để dựng lại — chọn bừa một khối là dựng chuyện.";

        hex = Hex.ChuanHoa(nguon[0].MasterSeed);
        bytes = Hex.Doc(nguon[0].MasterSeed);

        if (bytes is null)
            return string.IsNullOrWhiteSpace(nguon[0].MasterSeed)
                ? $"Vòng {VongHatGiongA1} chưa công bố hạt giống gốc (cổng chưa đóng), nên chưa dựng lại "
                    + "được chồng phiếu."
                : $"Hạt giống gốc vòng {VongHatGiongA1} không phải chuỗi hợp lệ, nên không dựng lại được "
                    + "chồng phiếu.";

        return null;
    }

    /// <summary>
    /// Thành phần chồng phiếu (quy mô, số vé trúng) để dựng lại. Ưu tiên con số <b>chính chồng phiếu
    /// khai lúc niêm phong</b>: dựng lại từ đó rồi so mã băm là bằng chứng mạnh hơn đếm ngược từ vé
    /// đã mở, vì bản đã mở chính là thứ đang bị nghi. Không khai thì mới đếm lại từ vé đã công bố.
    /// Quy mô <c>null</c> nghĩa là chưa dựng được — phần <c>Nguon</c> khi đó là lý do.
    /// </summary>
    private static (int? QuyMo, int? VeTrung, string Nguon) ThanhPhan(Deck deck)
    {
        var ve = deck.Tickets;

        if (deck.Size is { } quyMo && deck.WonCount is { } veTrung)
        {
            if (quyMo < 0 || veTrung < 0 || veTrung > quyMo)
                return (null, null, $"Chồng phiếu khai quy mô {quyMo} vé và {veTrung} vé trúng — bộ số vô lý, "
                    + "nên không dựng lại được chồng phiếu nào để đối chiếu.");

            if (quyMo > TranQuyMo)
                return (null, null, $"Chồng phiếu khai quy mô {quyMo} vé, vượt xa quy mô một dự án thật, nên "
                    + "công cụ không dựng lại — dựng theo con số đó là treo trình duyệt của người đọc.");

            return (quyMo, veTrung, "số liệu niêm phong của chính chồng phiếu (quy mô + số vé trúng)");
        }

        if (ve is null)
            return (null, null, "Chồng phiếu không khai đủ quy mô và số vé trúng, cũng chưa công bố nội dung "
                + "vé, nên không biết dựng lại chồng phiếu gồm những vé gì.");

        if (ve.Count > TranQuyMo)
            return (null, null, $"Chồng phiếu công bố {ve.Count} vé, vượt xa quy mô một dự án thật, nên công "
                + "cụ không dựng lại — dựng theo con số đó là treo trình duyệt của người đọc.");

        // Đếm lại thì mọi vé phải thuộc hai dạng vé của vòng quyền mua: gặp dạng lạ mà vẫn xếp nó
        // vào "không trúng" là công cụ tự bịa ra thành phần chồng phiếu rồi tự khớp với nó.
        if (ve.Any(v => v != LotteryLabels.A1Win && v != LotteryLabels.A1Lose))
            return (null, null, "Chồng phiếu vòng quyền mua có nội dung vé không thuộc hai dạng của vòng này, "
                + "nên không suy được thành phần để dựng lại.");

        return (ve.Count, ve.Count(v => v == LotteryLabels.A1Win),
            "đếm lại từ nội dung vé đã công bố (chồng phiếu không khai số vé trúng)");
    }

    private static string? Chuan(string? giaTri) =>
        string.IsNullOrWhiteSpace(giaTri) ? null : giaTri.Trim();
}
