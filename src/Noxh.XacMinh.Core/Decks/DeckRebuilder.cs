using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Units;

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

    // ── Riêng vòng phân căn ưu tiên ─────────────────────────────────────────

    /// <summary>Mã loại căn của chồng phiếu (vòng phân căn ưu tiên đi theo từng loại).</summary>
    public string? TypeCode { get; init; }

    public string? PoolSeedLabel { get; init; }

    public string? PoolSeed { get; init; }

    /// <summary>Số căn loại này trong danh mục đã dùng để dựng lại quỹ căn ưu tiên.</summary>
    public int? CatalogUnitCount { get; init; }

    /// <summary>
    /// Quỹ căn ưu tiên dựng lại từ hạt giống, theo <b>đúng thứ tự</b> hạt giống sinh ra — đây là
    /// phần quỹ đã chảy vào chồng phiếu này. Báo cáo minh bạch không công bố số suất ưu tiên của
    /// từng loại, nên công cụ chỉ khẳng định tới đúng số căn đã thành vé trúng.
    /// </summary>
    public IReadOnlyList<string>? PriorityUnits { get; init; }
}

/// <summary>
/// Dựng lại chồng phiếu từ hạt giống đã cam kết, bằng phép xáo copy nguyên văn từ backend
/// (<see cref="SeededShuffle"/>). Thuần, không I/O, tất định.
///
/// Đây là bước nhảy so với hạng mục mã băm chồng phiếu: mã băm chỉ chứng minh nội dung vé không đổi
/// <b>sau khi</b> niêm phong; dựng lại chứng minh chính chồng phiếu đó mọc ra từ hạt giống đã cam
/// kết <b>trước</b> lễ, nên không ai sắp đặt được vị trí vé trúng.
///
/// Đã dựng lại được vòng quyền mua (A1) và vòng phân căn ưu tiên (A2 từng loại căn — dựng lại cả
/// quỹ căn ưu tiên của loại đó rồi mới tới chồng phiếu). B/C còn cần quỹ căn còn dư suy từ kết quả
/// vòng trước; công cụ không dựng lại được thì <b>không kết luận</b> chứ không đoán.
/// </summary>
public static class DeckRebuilder
{
    /// <summary>Hạt giống gốc của A1 và A2 đều là hạt giống vòng A (gate A đóng sinh ra nó).</summary>
    private const string VongHatGiongA = "A";

    /// <summary>
    /// Trần quy mô chồng phiếu dựng lại. Dự án lớn nhất ~900 vé; con số trong file người dùng thả
    /// vào thì có thể bị sửa thành bất kỳ thứ gì — cấp phát theo nó là treo tab trình duyệt.
    /// </summary>
    private const int TranQuyMo = 100_000;

    /// <summary>Nhãn dẫn xuất hạt giống của chồng phiếu, <c>null</c> nếu công cụ chưa dựng lại được vòng đó.</summary>
    public static string? SeedLabel(string? round)
    {
        var vong = Chuan(round);
        if (vong == LotteryLabels.RoundA1) return LotteryLabels.A1Deck;

        return LoaiCanUuTien(vong) is { } loai ? LotteryLabels.A2Deck(loai) : null;
    }

    /// <summary>
    /// Dựng lại một chồng phiếu; <c>null</c> nếu công cụ chưa biết dựng lại vòng của nó. Danh mục căn
    /// là dữ liệu đầu vào do ban tổ chức công bố (vỏ giao diện đưa vào), chỉ vòng phân căn ưu tiên
    /// mới cần tới.
    /// </summary>
    public static DeckRebuild? Rebuild(TransparencyReport report, Deck deck, UnitCatalog? catalog = null)
    {
        var round = Chuan(deck.Round);

        if (round == LotteryLabels.RoundA1) return VongQuyenMua(report, deck, round!);

        return LoaiCanUuTien(round) is { } loai ? VongUuTien(report, deck, round!, loai, catalog) : null;
    }

    /// <summary>Mã loại căn của chồng phiếu vòng phân căn ưu tiên (<c>A2:{loại}</c>), <c>null</c> nếu không phải vòng đó.</summary>
    private static string? LoaiCanUuTien(string? round)
    {
        if (round is null || !round.StartsWith(LotteryLabels.RoundA2Prefix, StringComparison.Ordinal))
            return null;

        // "A2:" trống mã loại thì không biết lấy quỹ căn nào ra dựng — coi như chưa dựng lại được.
        return Chuan(round[LotteryLabels.RoundA2Prefix.Length..]);
    }

    private static DeckRebuild? VongQuyenMua(TransparencyReport report, Deck deck, string round)
    {
        var ketQua = new DeckRebuild(round, LotteryLabels.A1Deck, VongHatGiongA);

        if (HatGiongGoc(report, out var masterSeedHex, out var masterSeed) is { } thieuHatGiong)
            return ketQua with { MasterSeed = masterSeedHex, Blocker = thieuHatGiong };

        var roundSeed = MasterSeed.RoundSeed(masterSeed!, LotteryLabels.A1Deck);
        ketQua = ketQua with { MasterSeed = masterSeedHex, RoundSeed = Hex.ChuanHoa(Convert.ToHexString(roundSeed)) };

        var (quyMo, veTrung, nguon) = ThanhPhan(deck, VeVong.QuyenMua);
        if (quyMo is null) return ketQua with { Blocker = nguon };

        ketQua = ketQua with { Size = quyMo, WonCount = veTrung, CompositionSource = nguon };

        // Chồng phiếu A1 trước khi xáo: [TRÚNG QUYỀN MUA × w, KHÔNG TRÚNG ƯU TIÊN × (n − w)] —
        // đúng thứ tự A1DeckPlan.Compute dựng, vì thứ tự trước khi xáo cũng quyết định kết quả.
        var truocKhiXao = new List<string>(quyMo.Value);
        truocKhiXao.AddRange(Enumerable.Repeat(LotteryLabels.A1Win, veTrung!.Value));
        truocKhiXao.AddRange(Enumerable.Repeat(LotteryLabels.A1Lose, quyMo.Value - veTrung.Value));

        return Xao(ketQua, truocKhiXao, roundSeed);
    }

    /// <summary>
    /// Vòng phân căn ưu tiên: quỹ căn ưu tiên của loại L là <c>Shuffle(căn loại L, "POOL:{L}")</c>,
    /// vé trúng lấy lần lượt từ đầu quỹ đó, rồi cả chồng phiếu xáo bằng <c>"A2:deck:{L}"</c>. Chính
    /// vì quỹ mọc ra từ hạt giống nên việc <b>căn nào vào quỹ ưu tiên</b> cũng tái lập được, không
    /// phải do người sắp.
    /// </summary>
    private static DeckRebuild VongUuTien(
        TransparencyReport report, Deck deck, string round, string loai, UnitCatalog? danhMuc)
    {
        var ketQua = new DeckRebuild(round, LotteryLabels.A2Deck(loai), VongHatGiongA)
        {
            TypeCode = loai,
            PoolSeedLabel = LotteryLabels.PriorityPool(loai),
        };

        if (HatGiongGoc(report, out var masterSeedHex, out var masterSeed) is { } thieuHatGiong)
            return ketQua with { MasterSeed = masterSeedHex, Blocker = thieuHatGiong };

        var poolSeed = MasterSeed.RoundSeed(masterSeed!, LotteryLabels.PriorityPool(loai));
        var deckSeed = MasterSeed.RoundSeed(masterSeed!, LotteryLabels.A2Deck(loai));
        ketQua = ketQua with
        {
            MasterSeed = masterSeedHex,
            RoundSeed = Hex.ChuanHoa(Convert.ToHexString(deckSeed)),
            PoolSeed = Hex.ChuanHoa(Convert.ToHexString(poolSeed)),
        };

        if (danhMuc is null)
            return ketQua with
            {
                Blocker = "Công cụ chưa có danh mục căn hộ của dự án này, mà quỹ căn ưu tiên loại "
                    + $"'{MoTaGiaTri.Gon(loai)}' phải dựng lại từ chính danh mục đó — nạp file danh mục căn "
                    + "do ban tổ chức công bố rồi kiểm lại.",
            };

        var quyCan = danhMuc.Types.FirstOrDefault(t => string.Equals(t.TypeCode, loai, StringComparison.Ordinal));
        if (quyCan is null)
            return ketQua with
            {
                Blocker = $"Danh mục căn đang dùng không có loại căn '{MoTaGiaTri.Gon(loai)}', nên không dựng "
                    + "lại được quỹ căn ưu tiên của loại này. Nhiều khả năng đây là danh mục của dự án khác — "
                    + "nạp đúng file danh mục căn của dự án đang kiểm rồi kiểm lại.",
            };

        ketQua = ketQua with { CatalogUnitCount = quyCan.UnitCodes.Count };

        var (quyMo, veTrung, nguon) = ThanhPhan(deck, VeVong.PhanCanUuTien);
        if (quyMo is null) return ketQua with { Blocker = nguon };

        ketQua = ketQua with { Size = quyMo, WonCount = veTrung, CompositionSource = nguon };

        if (KhopDanhMuc(deck, quyCan, veTrung!.Value, loai) is { } lechDanhMuc)
            return ketQua with { Blocker = lechDanhMuc };

        // Sắp ordinal trước khi xáo, đúng như backend: thứ tự dòng trong file danh mục người kiểm nạp
        // vào không được đổi kết quả dựng lại.
        var quyUuTien = SeededShuffle
            .Shuffle(quyCan.UnitCodes.Order(StringComparer.Ordinal).ToList(), poolSeed)
            .Take(veTrung.Value)
            .ToList();

        // Trước khi xáo: [TRÚNG:{căn} theo thứ tự quỹ ưu tiên, CHỜ PHÂN LOẠI DƯ × (n − w)].
        var truocKhiXao = new List<string>(quyMo.Value);
        truocKhiXao.AddRange(quyUuTien.Select(LotteryLabels.Win));
        truocKhiXao.AddRange(Enumerable.Repeat(LotteryLabels.A2Pending, quyMo.Value - veTrung.Value));

        return Xao(ketQua with { PriorityUnits = quyUuTien }, truocKhiXao, deckSeed);
    }

    private static DeckRebuild Xao(DeckRebuild ketQua, List<string> truocKhiXao, byte[] seed)
    {
        var tickets = SeededShuffle.Shuffle(truocKhiXao, seed);

        return ketQua with { Tickets = tickets, DeckHash = CanonicalDeckSerializer.Hash(tickets) };
    }

    /// <summary>
    /// Danh mục đang dùng có đủ căn để dựng lại quỹ ưu tiên của chồng phiếu này không. Căn đã công bố
    /// trong vé mà danh mục không có ⇒ danh mục này <b>không phải</b> danh mục của dự án đang kiểm;
    /// kết luận KHÔNG ĐẠT khi đó là vu oan một buổi lễ sạch, nên trả về lý do chưa kiểm được.
    /// </summary>
    private static string? KhopDanhMuc(Deck deck, UnitCatalogType quyCan, int veTrung, string loai)
    {
        if (veTrung > quyCan.UnitCodes.Count)
            return $"Chồng phiếu khai {veTrung} vé trúng, nhiều hơn số căn loại '{MoTaGiaTri.Gon(loai)}' trong "
                + $"danh mục đang dùng ({quyCan.UnitCodes.Count} căn), nên quỹ căn ưu tiên dựng lại không đủ "
                + "căn để đối chiếu — nạp đúng danh mục căn của dự án đang kiểm rồi kiểm lại.";

        var coTrongDanhMuc = quyCan.UnitCodes.ToHashSet(StringComparer.Ordinal);
        var lac = (deck.Tickets ?? [])
            .Where(v => v is not null && v.StartsWith(LotteryLabels.WinPrefix, StringComparison.Ordinal))
            .Select(v => v![LotteryLabels.WinPrefix.Length..])
            .FirstOrDefault(can => !coTrongDanhMuc.Contains(can));

        return lac is null
            ? null
            : $"Chồng phiếu công bố vé trúng căn '{MoTaGiaTri.Gon(lac)}', mà danh mục đang dùng không có căn "
                + $"đó trong loại '{MoTaGiaTri.Gon(loai)}' — danh mục này không phải danh mục của dự án đang "
                + "kiểm. Nạp đúng file danh mục căn rồi kiểm lại.";
    }

    /// <summary>Trả về lý do KHÔNG lấy được hạt giống gốc, <c>null</c> nếu lấy được.</summary>
    private static string? HatGiongGoc(TransparencyReport report, out string? hex, out byte[]? bytes)
    {
        hex = null;
        bytes = null;

        // Lọc ô rỗng: mảng JSON có phần tử `null` là file hỏng, không được thành ngoại lệ trắng trang.
        var nguon = (report.EntropySources ?? [])
            .Where(n => n is not null && Chuan(n.Round) == VongHatGiongA)
            .ToList();

        if (nguon.Count == 0)
            return $"Báo cáo không công bố nguồn ngẫu nhiên vòng {VongHatGiongA}, nên không có hạt giống "
                + "nào để dựng lại chồng phiếu.";

        if (nguon.Count > 1)
            return $"Báo cáo công bố nhiều khối nguồn ngẫu nhiên cùng mang tên vòng {VongHatGiongA}, nên "
                + "không biết lấy hạt giống nào để dựng lại — chọn bừa một khối là dựng chuyện.";

        hex = Hex.ChuanHoa(nguon[0].MasterSeed);
        bytes = Hex.Doc(nguon[0].MasterSeed);

        if (bytes is null)
            return string.IsNullOrWhiteSpace(nguon[0].MasterSeed)
                ? $"Vòng {VongHatGiongA} chưa công bố hạt giống gốc (cổng chưa đóng), nên chưa dựng lại "
                    + "được chồng phiếu."
                : $"Hạt giống gốc vòng {VongHatGiongA} không phải chuỗi hợp lệ, nên không dựng lại được "
                    + "chồng phiếu.";

        return null;
    }

    /// <summary>Hai dạng vé hợp lệ của một vòng, để đếm lại thành phần chồng phiếu từ vé đã mở.</summary>
    private sealed record VeVong(Func<string, bool> HopLe, Func<string, bool> Trung, string MoTa)
    {
        public static readonly VeVong QuyenMua = new(
            v => v is LotteryLabels.A1Win or LotteryLabels.A1Lose,
            v => v == LotteryLabels.A1Win,
            "vòng quyền mua");

        public static readonly VeVong PhanCanUuTien = new(
            v => v == LotteryLabels.A2Pending || v.StartsWith(LotteryLabels.WinPrefix, StringComparison.Ordinal),
            v => v.StartsWith(LotteryLabels.WinPrefix, StringComparison.Ordinal),
            "vòng phân căn ưu tiên");
    }

    /// <summary>
    /// Thành phần chồng phiếu (quy mô, số vé trúng) để dựng lại. Ưu tiên con số <b>chính chồng phiếu
    /// khai lúc niêm phong</b>: dựng lại từ đó rồi so mã băm là bằng chứng mạnh hơn đếm ngược từ vé
    /// đã mở, vì bản đã mở chính là thứ đang bị nghi. Không khai thì mới đếm lại từ vé đã công bố.
    /// Quy mô <c>null</c> nghĩa là chưa dựng được — phần <c>Nguon</c> khi đó là lý do.
    /// </summary>
    private static (int? QuyMo, int? VeTrung, string Nguon) ThanhPhan(Deck deck, VeVong dang)
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

        // Đếm lại thì mọi vé phải thuộc hai dạng vé của vòng đó: gặp dạng lạ mà vẫn xếp nó vào "không
        // trúng" là công cụ tự bịa ra thành phần chồng phiếu rồi tự khớp với nó.
        if (ve.Any(v => v is null || !dang.HopLe(v)))
            return (null, null, $"Chồng phiếu {dang.MoTa} có nội dung vé không thuộc hai dạng của vòng này, "
                + "nên không suy được thành phần để dựng lại.");

        return (ve.Count, ve.Count(v => dang.Trung(v!)),
            "đếm lại từ nội dung vé đã công bố (chồng phiếu không khai số vé trúng)");
    }

    private static string? Chuan(string? giaTri) =>
        string.IsNullOrWhiteSpace(giaTri) ? null : giaTri.Trim();
}
