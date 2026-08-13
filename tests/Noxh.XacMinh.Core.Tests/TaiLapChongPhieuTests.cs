using System.Text.Json.Nodes;
using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Decks;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #11 — tái lập chồng phiếu vòng quyền mua (A1) từ hạt giống. Khác hạng mục mã băm chồng phiếu:
/// ở đây công cụ <b>tự dựng lại</b> chồng phiếu bằng phép xáo copy nguyên văn từ backend rồi mới so
/// mã băm, nên nó chứng minh chồng phiếu mọc ra từ hạt giống đã cam kết chứ không do ai sắp đặt.
/// </summary>
public class TaiLapChongPhieuTests
{
    private const string VongQuyenMua = "A1";

    private static TransparencyReport Golden() => Parse(GoldenFixture.Json());

    private static TransparencyReport Parse(string json)
    {
        var result = TransparencyJson.Parse(json);
        Assert.True(result.Success, result.ErrorMessage);
        return result.Report!;
    }

    private static VerificationReport Verify(TransparencyReport report) =>
        Verifier.Verify(new VerificationInput(report));

    private static IReadOnlyList<CheckResult> TaiLapItems(VerificationReport report) =>
        report.Items.Where(i => i.Id.StartsWith(CheckIds.DeckRebuild, StringComparison.Ordinal)).ToList();

    private static CheckResult TaiLapA1(TransparencyReport report) =>
        Assert.Single(TaiLapItems(Verify(report)), i => i.Id == $"{CheckIds.DeckRebuild}:{VongQuyenMua}");

    /// <summary>Sửa JSON gốc bằng JsonNode — đúng thứ người dùng thả vào, không phải model đã nạp.</summary>
    private static string EditGolden(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(GoldenFixture.Json())!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    private static JsonObject NguonVong(JsonObject root, string vong) =>
        root["nguonNgauNhien"]!.AsArray().First(n => n!["round"]!.GetValue<string>() == vong)!.AsObject();

    private static JsonObject ChongPhieu(JsonObject root, string vong) =>
        root["decks"]!.AsArray().First(d => d!["round"]!.GetValue<string>() == vong)!.AsObject();

    /// <summary>Đổi đúng MỘT byte của chuỗi hex: lật bit thấp nhất của byte cuối.</summary>
    private static string DoiMotByte(string hex) =>
        hex[..^2] + (Convert.ToByte(hex[^2..], 16) ^ 0x01).ToString("x2");

    // ── AC1: chồng phiếu vòng quyền mua dựng lại từ hạt giống, so mã băm với bản công bố ──

    [Fact]
    public void AC1_FixtureChuanVang_ChongPhieuVongQuyenMua_DungLaiDuocVaKhopMaBamCongBo()
    {
        var golden = Golden();

        var item = TaiLapA1(golden);

        Assert.Equal(CheckStatus.Dat, item.Status);
        Assert.Equal(golden.Decks!.Single(d => d.Round == VongQuyenMua).DeckHash, item.Expected);
        Assert.Equal(item.Expected, item.Actual);
    }

    [Fact]
    public void AC1_BanDungLai_RaDungTungLaVeDangCongBo_ChuKhongChiTrungMaBam()
    {
        var golden = Golden();
        var deck = golden.Decks!.Single(d => d.Round == VongQuyenMua);

        var taiLap = DeckRebuilder.Rebuild(golden, deck);

        Assert.NotNull(taiLap);
        Assert.Equal(deck.Tickets, taiLap!.Tickets);
    }

    [Fact]
    public void AC1_HangMuc_MangPreimageCuaBanDungLai_DeNguoiKiemTuBamLai()
    {
        var item = TaiLapA1(Golden());

        Assert.False(string.IsNullOrWhiteSpace(item.Preimage));
        Assert.Equal(item.Actual, Hex.Sha256Hex(System.Text.Encoding.UTF8.GetBytes(item.Preimage!)));
    }

    [Fact]
    public void AC1_CacVongChuaBietDungLai_KhongDeRaKetLuanBia()
    {
        // Vòng C cần quỹ căn dư và số dự khuyết, chưa dựng lại được: im lặng còn hơn báo ĐẠT bừa.
        var golden = Golden();

        Assert.All(
            golden.Decks!.Where(d => d.Round == "C"),
            d => Assert.Null(DeckRebuilder.Rebuild(golden, d, DanhMucGolden.Doc())));
    }

    [Fact]
    public void AC1_BaoCaoKhongCoChongPhieuNaoDungLaiDuoc_KhongKiemDuoc_ChuKhongImLang()
    {
        var khongCoVongDungLaiDuoc = Parse(EditGolden(root =>
        {
            var decks = root["decks"]!.AsArray();
            foreach (var deck in decks.Where(d => KhongPhaiVongDungLaiDuoc(d!) == false).ToList())
                decks.Remove(deck);
        }));

        var item = Assert.Single(TaiLapItems(Verify(khongCoVongDungLaiDuoc)));

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.NotEmpty(item.Metrics);
    }

    private static bool KhongPhaiVongDungLaiDuoc(JsonNode deck)
    {
        var vong = deck["round"]!.GetValue<string>();

        return vong != VongQuyenMua
            && !vong.StartsWith("A2:", StringComparison.Ordinal)
            && !vong.StartsWith("B:", StringComparison.Ordinal);
    }

    [Fact]
    public void AC1_VongQuyenMuaChuaCongBoHatGiongGoc_KhongKiemDuoc_ChuKhongKhongDat()
    {
        var thieuHatGiong = Parse(EditGolden(root => NguonVong(root, "A")["masterSeed"] = null));

        Assert.Equal(CheckStatus.KhongKiemDuoc, TaiLapA1(thieuHatGiong).Status);
    }

    [Fact]
    public void AC1_ChongPhieuThieuMaBamNiemPhong_KhongKiemDuoc_ChuKhongLayBanDungLaiLamChuan()
    {
        var thieuHash = Parse(EditGolden(root => ChongPhieu(root, VongQuyenMua)["deckHash"] = null));

        Assert.Equal(CheckStatus.KhongKiemDuoc, TaiLapA1(thieuHash).Status);
    }

    // ── AC2: test vector ghim cho phép xáo, lấy từ backend ───────────────────────────────

    /// <summary>
    /// Vector ghim lấy nguyên từ bộ test của backend
    /// (<c>tests/Lottery.IntegrationTests/Features/Lottery/Engine/GoldenDeckHashTests.cs</c>):
    /// entropy ghim cứng + deck A1 (5 vé, 2 vé trúng) → <c>DeckHashHex</c> golden. Copy phép xáo
    /// sai một byte là test này đỏ ngay, không cần đợi fixture minh bạch.
    /// </summary>
    [Fact]
    public void AC2_PhepXao_KhopVectorGhimCuaBackend()
    {
        var rServerA = Convert.FromHexString(new string('1', 64));
        var rSupervisor = Convert.FromHexString(new string('a', 64));
        var blockHash = Convert.FromHexString("0000000000000000000320283a032748cef8227873ff4872689bf23f1cda83a5");

        var masterSeedA = MasterSeed.Build(rServerA, rSupervisor, blockHash);
        var deckSeed = MasterSeed.RoundSeed(masterSeedA, LotteryLabels.A1Deck);

        var truocKhiXao = Enumerable.Repeat(LotteryLabels.A1Win, 2)
            .Concat(Enumerable.Repeat(LotteryLabels.A1Lose, 3))
            .ToList();
        var deck = SeededShuffle.Shuffle(truocKhiXao, deckSeed);

        Assert.Equal(
            "1aae5865a324a225b818949313abdb1acb919c18ed2108714dcad84b59de9b87",
            CanonicalDeckSerializer.Hash(deck));
    }

    /// <summary>Vector ghim của phép dẫn xuất hạt giống — nhãn khác thì hạt giống phải khác.</summary>
    [Fact]
    public void AC2_HatGiongDanXuat_PhuThuocNhanDanXuat()
    {
        var master = Convert.FromHexString(new string('9', 64));

        var a1 = MasterSeed.RoundSeed(master, LotteryLabels.A1Deck);
        var khac = MasterSeed.RoundSeed(master, "A2:deck:T1");

        Assert.NotEqual(a1, khac);
        Assert.NotEqual(
            SeededShuffle.Shuffle(Enumerable.Range(0, 100).Select(i => $"T{i:D4}").ToList(), a1),
            SeededShuffle.Shuffle(Enumerable.Range(0, 100).Select(i => $"T{i:D4}").ToList(), khac));
    }

    [Fact]
    public void AC2_PhepXao_TatDinh_VaGiuNguyenThanhPhanChongPhieu()
    {
        var seed = Convert.FromHexString(new string('c', 64));
        var nguon = Enumerable.Repeat(LotteryLabels.A1Win, 3)
            .Concat(Enumerable.Repeat(LotteryLabels.A1Lose, 7))
            .ToList();

        var lan1 = SeededShuffle.Shuffle(nguon, seed);
        var lan2 = SeededShuffle.Shuffle(nguon, seed);

        Assert.Equal(lan1, lan2);
        Assert.Equal(nguon.Count, lan1.Count);
        Assert.Equal(3, lan1.Count(v => v == LotteryLabels.A1Win));
        Assert.Equal(nguon.Order(), lan1.Order());
    }

    // ── AC3: lưới ô phiếu đánh dấu ô khớp với bản dựng lại ───────────────────────────────

    [Fact]
    public void AC3_LuoiVongQuyenMua_MoiODeuDanhDauKhopBanDungLai()
    {
        var luoi = Assert.Single(DeckGridBuilder.Build(Golden()), l => l.Round == VongQuyenMua);

        Assert.All(luoi.Cells, o => Assert.True(o.MatchesRebuild));
        Assert.Equal(luoi.Cells.Count, luoi.Summary.CellsMatchingRebuild);
    }

    [Fact]
    public void AC3_TraoHaiVeTrongChongPhieu_ODoiChoLechHanBanDungLai()
    {
        var traoHaiVe = Parse(EditGolden(root =>
        {
            var ve = ChongPhieu(root, VongQuyenMua)["tickets"]!.AsArray();
            var dau = ve[0]!.GetValue<string>();
            var i = Enumerable.Range(1, ve.Count - 1).First(k => ve[k]!.GetValue<string>() != dau);
            (ve[0], ve[i]) = (JsonValue.Create(ve[i]!.GetValue<string>()), JsonValue.Create(dau));
        }));

        var luoi = Assert.Single(DeckGridBuilder.Build(traoHaiVe), l => l.Round == VongQuyenMua);

        Assert.Equal(2, luoi.Cells.Count(o => o.MatchesRebuild == false));
        Assert.Equal(luoi.Cells.Count - 2, luoi.Summary.CellsMatchingRebuild);
    }

    [Fact]
    public void AC3_VongChuaDungLaiDuoc_ODeTrong_ChuKhongDanhDauLaLech()
    {
        var luoi = DeckGridBuilder.Build(Golden()).Where(l => l.Round != VongQuyenMua).ToList();

        Assert.NotEmpty(luoi);
        Assert.All(luoi, l =>
        {
            Assert.All(l.Cells, o => Assert.Null(o.MatchesRebuild));
            Assert.Null(l.Summary.CellsMatchingRebuild);
        });
    }

    // ── AC4: chồng phiếu rỗng hợp lệ vẫn cho kết luận đúng, không lỗi ────────────────────

    [Fact]
    public void AC4_ChongPhieuRongHopLe_VanDat_KhongNemNgoaiLe()
    {
        var rong = new TransparencyReport
        {
            EntropySources = Golden().EntropySources,
            Decks =
            [
                new Deck
                {
                    Round = VongQuyenMua,
                    Size = 0,
                    WonCount = 0,
                    Tickets = [],
                    // SHA-256 của chuỗi rỗng: chồng phiếu 0 vé serialize ra 0 byte.
                    DeckHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                },
            ],
        };

        var item = TaiLapA1(rong);

        Assert.Equal(CheckStatus.Dat, item.Status);
        Assert.Empty(Assert.Single(DeckGridBuilder.Build(rong)).Cells);
    }

    [Fact]
    public void AC4_ChongPhieuKhaiQuyMoPhiLy_KhongKiemDuoc_ChuKhongTreoTrinhDuyet()
    {
        var phiLy = new TransparencyReport
        {
            EntropySources = Golden().EntropySources,
            Decks = [new Deck { Round = VongQuyenMua, Size = int.MaxValue, WonCount = 0, DeckHash = new string('0', 64) }],
        };

        Assert.Equal(CheckStatus.KhongKiemDuoc, TaiLapA1(phiLy).Status);
    }

    [Fact]
    public void AC4_ChongPhieuKhaiSoVeTrungNgoaiPhamVi_KhongKiemDuoc()
    {
        var voLy = Parse(EditGolden(root => ChongPhieu(root, VongQuyenMua)["wonCount"] = 99));

        Assert.Equal(CheckStatus.KhongKiemDuoc, TaiLapA1(voLy).Status);
    }

    // ── AC5: đổi một byte trong hạt giống → hạng mục tái lập chuyển KHÔNG ĐẠT ────────────

    [Fact]
    public void AC5_DoiMotByteTrongHatGiong_HangMucTaiLapChuyenKhongDat()
    {
        var lechHatGiong = Parse(EditGolden(root =>
        {
            var nguon = NguonVong(root, "A");
            nguon["masterSeed"] = DoiMotByte(nguon["masterSeed"]!.GetValue<string>());
        }));

        var item = TaiLapA1(lechHatGiong);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.NotEqual(item.Expected, item.Actual);
        Assert.Equal(CheckStatus.KhongDat, Verify(lechHatGiong).Overall);
    }

    [Fact]
    public void AC5_DoiMotByteTrongHatGiong_LuoiChiRaOLechBanDungLai()
    {
        var lechHatGiong = Parse(EditGolden(root =>
        {
            var nguon = NguonVong(root, "A");
            nguon["masterSeed"] = DoiMotByte(nguon["masterSeed"]!.GetValue<string>());
        }));

        var luoi = Assert.Single(DeckGridBuilder.Build(lechHatGiong), l => l.Round == VongQuyenMua);

        Assert.Contains(luoi.Cells, o => o.MatchesRebuild == false);
        Assert.True(luoi.Summary.CellsMatchingRebuild < luoi.Cells.Count);
    }

    /// <summary>Đổi hạt giống mà mã băm chồng phiếu vẫn ĐẠT — đó chính là chỗ hạng mục này bắt được.</summary>
    [Fact]
    public void AC5_DoiHatGiong_MaBamChongPhieuVanDat_ChiHangMucTaiLapBatDuoc()
    {
        var lechHatGiong = Parse(EditGolden(root =>
        {
            var nguon = NguonVong(root, "A");
            nguon["masterSeed"] = DoiMotByte(nguon["masterSeed"]!.GetValue<string>());
        }));

        var items = Verify(lechHatGiong).Items;

        Assert.Equal(CheckStatus.Dat, items.Single(i => i.Id == $"{CheckIds.DeckHash}:{VongQuyenMua}").Status);
        Assert.Equal(CheckStatus.KhongDat, items.Single(i => i.Id == $"{CheckIds.DeckRebuild}:{VongQuyenMua}").Status);
    }

    // ── AC6: chế độ chuyên sâu hiện nhãn dẫn xuất hạt giống đã dùng ──────────────────────

    [Fact]
    public void AC6_SoLieuTho_MangNhanDanXuatHatGiongVaHaiHatGiongDaDung()
    {
        var golden = Golden();
        var item = TaiLapA1(golden);
        var masterSeedA = golden.EntropySources!.Single(n => n.Round == "A").MasterSeed;

        Assert.Equal(
            LotteryLabels.A1Deck,
            Assert.Single(item.Metrics, m => m.Label == "Nhãn dẫn xuất hạt giống").Value);
        Assert.Equal(
            masterSeedA,
            Assert.Single(item.Metrics, m => m.Label.Contains("MASTER_SEED", StringComparison.Ordinal)).Value);
        Assert.Contains(item.Metrics, m => m.Label.Contains("dẫn xuất của chồng phiếu", StringComparison.Ordinal));
    }

    [Fact]
    public void AC6_SoLieuTho_NoiRoThanhPhanDungLai_LayTuDau()
    {
        var item = TaiLapA1(Golden());

        Assert.Equal("6", Assert.Single(item.Metrics, m => m.Label == "Quy mô dùng để dựng lại").Value);
        Assert.Equal("4", Assert.Single(item.Metrics, m => m.Label == "Số vé trúng dùng để dựng lại").Value);
        Assert.False(string.IsNullOrWhiteSpace(
            Assert.Single(item.Metrics, m => m.Label == "Nguồn thành phần chồng phiếu").Value));
    }

    [Fact]
    public void AC6_ChongPhieuKhongKhaiSoVeTrung_VanDungLaiDuocBangCachDemLaiNoiDungVe()
    {
        var khongKhai = Parse(EditGolden(root => ChongPhieu(root, VongQuyenMua).Remove("wonCount")));

        var item = TaiLapA1(khongKhai);

        Assert.Equal(CheckStatus.Dat, item.Status);
        Assert.Contains("đếm", Assert.Single(item.Metrics, m => m.Label == "Nguồn thành phần chồng phiếu").Value);
    }
}
