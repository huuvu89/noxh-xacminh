using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Units;

namespace Noxh.XacMinh.Core.Decks;

/// <summary>Vòng nào đang được dựng lại — mỗi vòng có một cách dựng quỹ căn khác nhau.</summary>
public enum LoaiTaiLap
{
    QuyenMua,
    PhanCanUuTien,
    BocThangTheoLoai,
    CanDuVaDuKhuyet,
}

/// <summary>
/// Kết quả dựng lại một chồng phiếu từ hạt giống đã công bố. <see cref="Tickets"/> có giá trị là
/// dựng được; <see cref="Blocker"/> có giá trị là <b>chưa dựng được vì thiếu/hỏng dữ liệu</b> —
/// không phải "không khớp". Hai ca đó phải ra hai kết luận khác nhau.
/// </summary>
public sealed record DeckRebuild(string Round, string SeedLabel, string EntropyRound, LoaiTaiLap Kind)
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

    /// <summary>
    /// Giả định mà bản dựng lại này đang đứng trên (dữ liệu báo cáo không công bố, công cụ phải hiểu
    /// theo một cách). Có giả định thì <b>lệch mã băm không được kết luận KHÔNG ĐẠT</b>: lệch có thể
    /// là do giả định sai chứ không phải do buổi lễ sai.
    /// </summary>
    public string? Assumption { get; init; }

    // ── Riêng các vòng đi theo loại căn (phân căn ưu tiên, bốc thẳng) ───────

    /// <summary>Mã loại căn của chồng phiếu (hai vòng này đều đi theo từng loại).</summary>
    public string? TypeCode { get; init; }

    public string? PoolSeedLabel { get; init; }

    public string? PoolSeed { get; init; }

    /// <summary>Số căn loại này trong danh mục đã dùng để dựng lại quỹ căn.</summary>
    public int? CatalogUnitCount { get; init; }

    /// <summary>
    /// Quỹ căn dựng lại từ hạt giống, theo <b>đúng thứ tự</b> hạt giống sinh ra — đây là phần quỹ đã
    /// chảy vào chồng phiếu này. Báo cáo minh bạch không công bố số suất của từng loại, nên công cụ
    /// chỉ khẳng định tới đúng số căn đã thành vé trúng.
    /// </summary>
    public IReadOnlyList<string>? PoolUnits { get; init; }

    // ── Riêng vòng bốc thẳng theo loại căn ──────────────────────────────────

    /// <summary>
    /// Căn loại này đã được phân ở vòng trước — <b>suy ra</b> từ bảng kết quả, không phải dữ liệu ban
    /// tổ chức công bố. Sắp theo mã căn.
    /// </summary>
    public IReadOnlyList<string>? AllocatedBefore { get; init; }

    /// <summary>
    /// Quỹ căn còn dư của loại này = danh mục trừ đi <see cref="AllocatedBefore"/>, sắp theo mã căn.
    /// Cũng là <b>suy diễn</b>: đây là danh sách đem xáo để dựng lại chồng phiếu vòng bốc thẳng.
    /// </summary>
    public IReadOnlyList<string>? LeftoverUnits { get; init; }

    // ── Riêng vòng căn dư (vòng cuối) ───────────────────────────────────────

    /// <summary>Nhãn dẫn xuất hoán vị số dự khuyết — nhãn riêng, tách khỏi nhãn xáo chồng phiếu.</summary>
    public string? WaitlistSeedLabel { get; init; }

    public string? WaitlistSeed { get; init; }

    /// <summary>
    /// Quy mô danh sách dự khuyết của dự án, lấy từ báo cáo. <c>null</c> = báo cáo không công bố, khi
    /// đó bản dựng lại hiểu là không giới hạn và mang theo <see cref="Assumption"/>.
    /// </summary>
    public int? WaitlistSize { get; init; }

    public int? WaitlistCount { get; init; }

    /// <summary>
    /// Số dự khuyết dựng lại, theo <b>thứ tự các ô dự khuyết trong chồng phiếu</b>. Đây là hoán vị
    /// 1..wl mọc ra từ hạt giống rồi gán vào ô theo vị trí tăng dần — không liên quan gì tới thứ tự
    /// bấm, nên bấm sớm không đổi được hạng dự khuyết.
    /// </summary>
    public IReadOnlyList<int>? WaitlistNumbers { get; init; }
}

/// <summary>
/// Dựng lại chồng phiếu từ hạt giống đã cam kết, bằng phép xáo copy nguyên văn từ backend
/// (<see cref="SeededShuffle"/>). Thuần, không I/O, tất định.
///
/// Đây là bước nhảy so với hạng mục mã băm chồng phiếu: mã băm chỉ chứng minh nội dung vé không đổi
/// <b>sau khi</b> niêm phong; dựng lại chứng minh chính chồng phiếu đó mọc ra từ hạt giống đã cam
/// kết <b>trước</b> lễ, nên không ai sắp đặt được vị trí vé trúng.
///
/// Dựng lại được cả bốn vòng: quyền mua (A1), phân căn ưu tiên (A2 từng loại căn — dựng lại cả quỹ
/// căn ưu tiên của loại đó rồi mới tới chồng phiếu), bốc thẳng theo loại căn (B từng loại — quỹ căn
/// còn dư phải <b>suy ra</b> từ căn đã phân ở vòng trước) và căn dư (C — một quỹ chung, cộng thêm
/// hoán vị số dự khuyết). Vòng nào chưa dựng lại được thì <b>không kết luận</b> chứ không đoán.
/// </summary>
public static class DeckRebuilder
{
    /// <summary>Hạt giống gốc của A1 và A2 đều là hạt giống vòng A (gate A đóng sinh ra nó).</summary>
    private const string VongHatGiongA = "A";

    /// <summary>Vòng bốc thẳng có hạt giống riêng, sinh ra khi gate B đóng.</summary>
    private const string VongHatGiongB = "B";

    /// <summary>
    /// Trần quy mô chồng phiếu dựng lại. Dự án lớn nhất ~900 vé; con số trong file người dùng thả
    /// vào thì có thể bị sửa thành bất kỳ thứ gì — cấp phát theo nó là treo tab trình duyệt.
    /// </summary>
    private const int TranQuyMo = 100_000;

    /// <summary>
    /// Dựng lại một chồng phiếu; <c>null</c> nếu công cụ chưa biết dựng lại vòng của nó. Danh mục căn
    /// là dữ liệu đầu vào do ban tổ chức công bố (vỏ giao diện đưa vào), chỉ hai vòng đi theo loại căn
    /// mới cần tới.
    /// </summary>
    public static DeckRebuild? Rebuild(TransparencyReport report, Deck deck, UnitCatalog? catalog = null)
    {
        var round = Chuan(deck.Round);

        if (round == LotteryLabels.RoundA1) return VongQuyenMua(report, deck, round!);

        if (round == LotteryLabels.RoundC) return VongCanDu(report, deck, round!, catalog);

        if (LoaiCan(round, LotteryLabels.RoundA2Prefix) is { } loaiUuTien)
            return VongUuTien(report, deck, round!, loaiUuTien, catalog);

        return LoaiCan(round, LotteryLabels.RoundBPrefix) is { } loaiBocThang
            ? VongBocThang(report, deck, round!, loaiBocThang, catalog)
            : null;
    }

    /// <summary>Mã loại căn của chồng phiếu đi theo loại (<c>{tiền tố}{loại}</c>), <c>null</c> nếu không phải vòng đó.</summary>
    private static string? LoaiCan(string? round, string tienTo)
    {
        if (round is null || !round.StartsWith(tienTo, StringComparison.Ordinal)) return null;

        // Tiền tố trống mã loại thì không biết lấy quỹ căn nào ra dựng — coi như chưa dựng lại được.
        return Chuan(round[tienTo.Length..]);
    }

    private static DeckRebuild VongQuyenMua(TransparencyReport report, Deck deck, string round)
    {
        var ketQua = new DeckRebuild(round, LotteryLabels.A1Deck, VongHatGiongA, LoaiTaiLap.QuyenMua);

        if (HatGiongGoc(report, VongHatGiongA, out var masterSeedHex, out var masterSeed) is { } thieuHatGiong)
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
        var ketQua = new DeckRebuild(round, LotteryLabels.A2Deck(loai), VongHatGiongA, LoaiTaiLap.PhanCanUuTien)
        {
            TypeCode = loai,
            PoolSeedLabel = LotteryLabels.PriorityPool(loai),
        };

        if (HatGiongGoc(report, VongHatGiongA, out var masterSeedHex, out var masterSeed) is { } thieuHatGiong)
            return ketQua with { MasterSeed = masterSeedHex, Blocker = thieuHatGiong };

        var poolSeed = MasterSeed.RoundSeed(masterSeed!, LotteryLabels.PriorityPool(loai));
        var deckSeed = MasterSeed.RoundSeed(masterSeed!, LotteryLabels.A2Deck(loai));
        ketQua = ketQua with
        {
            MasterSeed = masterSeedHex,
            RoundSeed = Hex.ChuanHoa(Convert.ToHexString(deckSeed)),
            PoolSeed = Hex.ChuanHoa(Convert.ToHexString(poolSeed)),
        };

        if (LayQuyCan(danhMuc, loai, "quỹ căn ưu tiên", out var quyCan) is { } thieuDanhMuc)
            return ketQua with { Blocker = thieuDanhMuc };

        ketQua = ketQua with { CatalogUnitCount = quyCan!.UnitCodes.Count };

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

        return Xao(ketQua with { PoolUnits = quyUuTien }, truocKhiXao, deckSeed);
    }

    /// <summary>
    /// Vòng bốc thẳng theo loại căn: quỹ căn còn dư của loại L = danh mục loại L trừ những căn đã phân
    /// ở vòng trước (<see cref="QuyCanConDu"/> suy ra từ bảng kết quả), xáo bằng <c>"B:units:{L}"</c>;
    /// vé trúng lấy lần lượt từ đầu quỹ đó, rồi cả chồng phiếu xáo bằng <c>"B:deck:{L}"</c>.
    ///
    /// Khác vòng phân căn ưu tiên ở một điểm quyết định: quỹ căn ở đây đứng trên một <b>suy diễn</b>
    /// của công cụ. Suy diễn không nhất quán thì trả về <c>Blocker</c> — kết luận KHÔNG ĐẠT dựa trên
    /// suy diễn của chính mình là vu oan một buổi lễ sạch.
    /// </summary>
    private static DeckRebuild VongBocThang(
        TransparencyReport report, Deck deck, string round, string loai, UnitCatalog? danhMuc)
    {
        var ketQua = new DeckRebuild(round, LotteryLabels.BDeck(loai), VongHatGiongB, LoaiTaiLap.BocThangTheoLoai)
        {
            TypeCode = loai,
            PoolSeedLabel = LotteryLabels.LeftoverUnits(loai),
        };

        if (HatGiongGoc(report, VongHatGiongB, out var masterSeedHex, out var masterSeed) is { } thieuHatGiong)
            return ketQua with { MasterSeed = masterSeedHex, Blocker = thieuHatGiong };

        var poolSeed = MasterSeed.RoundSeed(masterSeed!, LotteryLabels.LeftoverUnits(loai));
        var deckSeed = MasterSeed.RoundSeed(masterSeed!, LotteryLabels.BDeck(loai));
        ketQua = ketQua with
        {
            MasterSeed = masterSeedHex,
            RoundSeed = Hex.ChuanHoa(Convert.ToHexString(deckSeed)),
            PoolSeed = Hex.ChuanHoa(Convert.ToHexString(poolSeed)),
        };

        if (LayQuyCan(danhMuc, loai, "quỹ căn còn dư", out var quyCan) is { } thieuDanhMuc)
            return ketQua with { Blocker = thieuDanhMuc };

        ketQua = ketQua with { CatalogUnitCount = quyCan!.UnitCodes.Count };

        var (daPhan, mauThuan) = QuyCanConDu.DaPhanTruocVongBocThang(report, danhMuc!);
        if (mauThuan is not null) return ketQua with { Blocker = mauThuan };

        var daPhanRoi = daPhan!.ToHashSet(StringComparer.Ordinal);
        // Sắp ordinal trước khi xáo, đúng như backend: thứ tự dòng trong file danh mục người kiểm nạp
        // vào không được đổi kết quả dựng lại.
        var conDu = quyCan.UnitCodes.Order(StringComparer.Ordinal).Where(c => !daPhanRoi.Contains(c)).ToList();
        ketQua = ketQua with
        {
            AllocatedBefore = quyCan.UnitCodes.Where(daPhanRoi.Contains).Order(StringComparer.Ordinal).ToList(),
            LeftoverUnits = conDu,
        };

        var (quyMo, veTrung, nguon) = ThanhPhan(deck, VeVong.BocThangTheoLoai);
        if (quyMo is null) return ketQua with { Blocker = nguon };

        ketQua = ketQua with { Size = quyMo, WonCount = veTrung, CompositionSource = nguon };

        if (KhopQuyConDu(deck, conDu, veTrung!.Value, loai) is { } lechQuy)
            return ketQua with { Blocker = lechQuy };

        var quyDaXao = SeededShuffle.Shuffle(conDu, poolSeed).Take(veTrung.Value).ToList();

        // Trước khi xáo: [TRÚNG:{căn} theo thứ tự quỹ căn còn dư, KHÔNG TRÚNG × (n − w)].
        var truocKhiXao = new List<string>(quyMo.Value);
        truocKhiXao.AddRange(quyDaXao.Select(LotteryLabels.Win));
        truocKhiXao.AddRange(Enumerable.Repeat(LotteryLabels.BLose, quyMo.Value - veTrung.Value));

        return Xao(ketQua with { PoolUnits = quyDaXao }, truocKhiXao, deckSeed);
    }

    /// <summary>
    /// Vòng căn dư (vòng cuối): <b>một</b> chồng phiếu duy nhất trên quỹ căn dư chung — mọi căn chưa
    /// ai nhận sau vòng bốc thẳng, không chia theo loại. Vé trúng lấy lần lượt từ đầu quỹ đã xáo bằng
    /// <c>"C:units"</c>, cả chồng phiếu xáo bằng <c>"C:deck"</c>, rồi các ô dự khuyết được đánh số
    /// bằng một hoán vị 1..wl xáo riêng bằng <c>"C:waitlist"</c> và gán theo vị trí ô tăng dần.
    ///
    /// Chính nhãn hạt giống riêng đó là điều đáng dựng lại nhất của cả công cụ: số dự khuyết không
    /// mọc ra từ vị trí trong chồng phiếu (thứ do thứ tự bấm quyết định), nên bấm sớm không được hạng
    /// nhỏ hơn. Dựng lại đúng từng số là chứng minh trực tiếp điều đó với người dân.
    ///
    /// Quy mô danh sách dự khuyết là dữ liệu của dự án, KHÔNG nằm trong mã băm đầu vào — phải lấy từ
    /// báo cáo. Báo cáo không công bố thì công cụ hiểu là không giới hạn, nhưng đánh dấu đó là
    /// <see cref="DeckRebuild.Assumption"/> để lệch mã băm không bị kết luận thành KHÔNG ĐẠT.
    /// </summary>
    private static DeckRebuild VongCanDu(
        TransparencyReport report, Deck deck, string round, UnitCatalog? danhMuc)
    {
        var ketQua = new DeckRebuild(round, LotteryLabels.CDeck, LotteryLabels.RoundC, LoaiTaiLap.CanDuVaDuKhuyet)
        {
            PoolSeedLabel = LotteryLabels.CUnits,
            WaitlistSeedLabel = LotteryLabels.CWaitlist,
            WaitlistSize = report.WaitlistSize,
        };

        if (HatGiongGoc(report, LotteryLabels.RoundC, out var masterSeedHex, out var masterSeed) is { } thieuHatGiong)
            return ketQua with { MasterSeed = masterSeedHex, Blocker = thieuHatGiong };

        var poolSeed = MasterSeed.RoundSeed(masterSeed!, LotteryLabels.CUnits);
        var deckSeed = MasterSeed.RoundSeed(masterSeed!, LotteryLabels.CDeck);
        var waitlistSeed = MasterSeed.RoundSeed(masterSeed!, LotteryLabels.CWaitlist);
        ketQua = ketQua with
        {
            MasterSeed = masterSeedHex,
            RoundSeed = Hex.ChuanHoa(Convert.ToHexString(deckSeed)),
            PoolSeed = Hex.ChuanHoa(Convert.ToHexString(poolSeed)),
            WaitlistSeed = Hex.ChuanHoa(Convert.ToHexString(waitlistSeed)),
        };

        if (danhMuc is null)
            return ketQua with
            {
                Blocker = "Công cụ chưa có danh mục căn hộ của dự án này, mà quỹ căn dư của vòng cuối phải dựng "
                    + "lại từ chính danh mục đó — nạp file danh mục căn do ban tổ chức công bố rồi kiểm lại.",
            };

        ketQua = ketQua with { CatalogUnitCount = danhMuc.Types.Sum(t => t.UnitCodes.Count) };

        var (daPhan, mauThuan) = QuyCanConDu.DaPhanTruocVongCanDu(report, danhMuc);
        if (mauThuan is not null) return ketQua with { Blocker = mauThuan };

        var daPhanRoi = daPhan!.ToHashSet(StringComparer.Ordinal);
        // Một quỹ chung, sắp ordinal theo mã căn xuyên loại — đúng như máy chủ lấy căn chưa ai nhận.
        var canDu = danhMuc.Types
            .SelectMany(t => t.UnitCodes)
            .Where(c => !daPhanRoi.Contains(c))
            .Order(StringComparer.Ordinal)
            .ToList();
        ketQua = ketQua with { AllocatedBefore = daPhan, LeftoverUnits = canDu };

        var (quyMo, veTrungKhai, nguon) = ThanhPhan(deck, VeVong.CanDu);
        if (quyMo is null) return ketQua with { Blocker = nguon };

        // Số vé trúng vòng cuối KHÔNG phải con số tự do: máy chủ đưa hết quỹ căn dư vào chồng phiếu,
        // nên nó luôn bằng min(quy mô, số căn dư). Lệch ⇒ suy diễn quỹ căn dư sai (hoặc bảng kết quả
        // đã bị sửa), mà công cụ không phân biệt được hai ca đó — nói chưa kiểm được, không kết luận.
        var veTrung = Math.Min(quyMo.Value, canDu.Count);
        ketQua = ketQua with { Size = quyMo, WonCount = veTrung, CompositionSource = nguon };

        if (veTrung != veTrungKhai)
            return ketQua with
            {
                Blocker = $"Chồng phiếu vòng cuối khai {veTrungKhai} vé trúng, còn quỹ căn dư công cụ suy ra "
                    + $"({canDu.Count} căn) trên quy mô {quyMo} vé chỉ sinh ra được {veTrung} vé trúng — suy diễn "
                    + "quỹ căn dư và chồng phiếu công bố mâu thuẫn nhau, nên không đối chiếu được.",
            };

        if (KhopQuyCanDu(deck, canDu) is { } lechQuy) return ketQua with { Blocker = lechQuy };

        if (report.WaitlistSize is { } quyMoDuKhuyet && quyMoDuKhuyet < 0)
            return ketQua with
            {
                Blocker = $"Báo cáo khai quy mô danh sách dự khuyết là {quyMoDuKhuyet} — con số vô lý, nên công "
                    + "cụ không dựng lại chồng phiếu vòng cuối theo nó.",
            };

        // Báo cáo bỏ trống quy mô dự khuyết đúng là cách dự án "không giới hạn" trông ra, nên hiểu vậy
        // là hiểu đúng máy chủ — nhưng vẫn phải nói ra là công cụ đang giả định.
        var tranDuKhuyet = report.WaitlistSize ?? int.MaxValue;
        var soDuKhuyet = Math.Min(quyMo.Value - veTrung, tranDuKhuyet);
        ketQua = ketQua with
        {
            WaitlistCount = soDuKhuyet,
            // Không có ô dự khuyết nào thì trần dự khuyết không đụng tới bản dựng lại — khi đó đừng
            // gắn giả định, kẻo một chồng phiếu bị sắp đặt lại được hạ xuống "chưa kiểm được".
            Assumption = report.WaitlistSize is null && soDuKhuyet > 0
                ? "báo cáo không công bố quy mô danh sách dự khuyết, nên công cụ dựng lại theo giả định danh "
                    + "sách dự khuyết KHÔNG giới hạn — đúng như một dự án bỏ trống con số đó"
                : null,
        };

        var quyDaXao = SeededShuffle.Shuffle(canDu, poolSeed).Take(veTrung).ToList();

        // Trước khi xáo: [TRÚNG:{căn} theo thứ tự quỹ căn dư, DỰ KHUYẾT chưa đánh số × wl, KHÔNG TRÚNG].
        var truocKhiXao = new List<string>(quyMo.Value);
        truocKhiXao.AddRange(quyDaXao.Select(LotteryLabels.Win));
        truocKhiXao.AddRange(Enumerable.Repeat(LotteryLabels.WaitlistPlaceholder, soDuKhuyet));
        truocKhiXao.AddRange(Enumerable.Repeat(LotteryLabels.CLose, quyMo.Value - veTrung - soDuKhuyet));

        var daXao = SeededShuffle.Shuffle(truocKhiXao, deckSeed);
        var so = SeededShuffle.Shuffle(Enumerable.Range(1, soDuKhuyet).ToList(), waitlistSeed);

        var k = 0;
        var ve = daXao
            .Select(v => v == LotteryLabels.WaitlistPlaceholder ? LotteryLabels.WaitlistTicket(so[k++]) : v)
            .ToList();

        return ketQua with
        {
            PoolUnits = quyDaXao,
            WaitlistNumbers = so,
            Tickets = ve,
            DeckHash = CanonicalDeckSerializer.Hash(ve),
        };
    }

    /// <summary>
    /// Vé trúng đang công bố có nằm trong quỹ căn dư suy ra không. Nằm ngoài nghĩa là hoặc suy diễn
    /// sai, hoặc căn đó đã được phân hai lần — không phân biệt được, nên chưa kiểm được.
    /// </summary>
    private static string? KhopQuyCanDu(Deck deck, List<string> canDu)
    {
        var trongQuy = canDu.ToHashSet(StringComparer.Ordinal);
        var lac = (deck.Tickets ?? [])
            .Where(v => v is not null && v.StartsWith(LotteryLabels.WinPrefix, StringComparison.Ordinal))
            .Select(v => v![LotteryLabels.WinPrefix.Length..])
            .FirstOrDefault(can => !trongQuy.Contains(can));

        return lac is null
            ? null
            : $"Chồng phiếu vòng cuối công bố vé trúng căn '{MoTaGiaTri.Gon(lac)}', mà căn đó không nằm trong "
                + "quỹ căn dư công cụ suy ra được — hoặc suy diễn sai, hoặc căn này đã được phân hai lần. Công "
                + "cụ không phân biệt được nên không kết luận đạt hay không đạt.";
    }

    /// <summary>Trả về lý do KHÔNG lấy được quỹ căn của loại này trong danh mục, <c>null</c> nếu lấy được.</summary>
    private static string? LayQuyCan(UnitCatalog? danhMuc, string loai, string tenQuy, out UnitCatalogType? quyCan)
    {
        quyCan = null;

        if (danhMuc is null)
            return $"Công cụ chưa có danh mục căn hộ của dự án này, mà {tenQuy} loại '{MoTaGiaTri.Gon(loai)}' "
                + "phải dựng lại từ chính danh mục đó — nạp file danh mục căn do ban tổ chức công bố rồi kiểm lại.";

        quyCan = danhMuc.Types.FirstOrDefault(t => string.Equals(t.TypeCode, loai, StringComparison.Ordinal));

        return quyCan is not null
            ? null
            : $"Danh mục căn đang dùng không có loại căn '{MoTaGiaTri.Gon(loai)}', nên không dựng lại được "
                + $"{tenQuy} của loại này. Nhiều khả năng đây là danh mục của dự án khác — nạp đúng file danh "
                + "mục căn của dự án đang kiểm rồi kiểm lại.";
    }

    /// <summary>
    /// Quỹ căn còn dư suy ra có nuôi nổi chồng phiếu đang công bố không. Vé trúng công bố một căn nằm
    /// ngoài quỹ suy ra nghĩa là hoặc suy diễn sai, hoặc buổi lễ phân căn đó hai lần — công cụ không
    /// phân biệt được hai ca đó, nên trả về lý do chưa kiểm được thay vì kết luận KHÔNG ĐẠT.
    /// </summary>
    private static string? KhopQuyConDu(Deck deck, List<string> conDu, int veTrung, string loai)
    {
        if (veTrung > conDu.Count)
            return $"Chồng phiếu khai {veTrung} vé trúng, nhiều hơn số căn loại '{MoTaGiaTri.Gon(loai)}' còn dư "
                + $"suy ra được từ vòng trước ({conDu.Count} căn), nên quỹ căn còn dư dựng lại không đủ căn để "
                + "đối chiếu — suy diễn quỹ căn còn dư và chồng phiếu công bố mâu thuẫn nhau.";

        var trongQuy = conDu.ToHashSet(StringComparer.Ordinal);
        var lac = (deck.Tickets ?? [])
            .Where(v => v is not null && v.StartsWith(LotteryLabels.WinPrefix, StringComparison.Ordinal))
            .Select(v => v![LotteryLabels.WinPrefix.Length..])
            .FirstOrDefault(can => !trongQuy.Contains(can));

        return lac is null
            ? null
            : $"Chồng phiếu công bố vé trúng căn '{MoTaGiaTri.Gon(lac)}', mà căn đó không nằm trong quỹ căn còn "
                + $"dư loại '{MoTaGiaTri.Gon(loai)}' công cụ suy ra được — hoặc suy diễn sai, hoặc căn này đã "
                + "được phân hai lần. Công cụ không phân biệt được nên không kết luận đạt hay không đạt.";
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

    /// <summary>Trả về lý do KHÔNG lấy được hạt giống gốc của vòng entropy này, <c>null</c> nếu lấy được.</summary>
    private static string? HatGiongGoc(TransparencyReport report, string vong, out string? hex, out byte[]? bytes)
    {
        hex = null;
        bytes = null;

        // Lọc ô rỗng: mảng JSON có phần tử `null` là file hỏng, không được thành ngoại lệ trắng trang.
        var nguon = (report.EntropySources ?? [])
            .Where(n => n is not null && Chuan(n.Round) == vong)
            .ToList();

        if (nguon.Count == 0)
            return $"Báo cáo không công bố nguồn ngẫu nhiên vòng {vong}, nên không có hạt giống nào để dựng "
                + "lại chồng phiếu.";

        if (nguon.Count > 1)
            return $"Báo cáo công bố nhiều khối nguồn ngẫu nhiên cùng mang tên vòng {vong}, nên không biết "
                + "lấy hạt giống nào để dựng lại — chọn bừa một khối là dựng chuyện.";

        hex = Hex.ChuanHoa(nguon[0].MasterSeed);
        bytes = Hex.Doc(nguon[0].MasterSeed);

        if (bytes is null)
            return string.IsNullOrWhiteSpace(nguon[0].MasterSeed)
                ? $"Vòng {vong} chưa công bố hạt giống gốc (cổng chưa đóng), nên chưa dựng lại được chồng phiếu."
                : $"Hạt giống gốc vòng {vong} không phải chuỗi hợp lệ, nên không dựng lại được chồng phiếu.";

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

        public static readonly VeVong BocThangTheoLoai = new(
            v => v == LotteryLabels.BLose || v.StartsWith(LotteryLabels.WinPrefix, StringComparison.Ordinal),
            v => v.StartsWith(LotteryLabels.WinPrefix, StringComparison.Ordinal),
            "vòng bốc thẳng theo loại căn");

        /// <summary>Vòng cuối là vòng duy nhất có ba dạng vé: trúng căn, dự khuyết có số, không trúng.</summary>
        public static readonly VeVong CanDu = new(
            v => v == LotteryLabels.CLose
                || v.StartsWith(LotteryLabels.WinPrefix, StringComparison.Ordinal)
                || v.StartsWith(LotteryLabels.WaitlistPrefix, StringComparison.Ordinal),
            v => v.StartsWith(LotteryLabels.WinPrefix, StringComparison.Ordinal),
            "vòng căn dư");
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
