using System.Text;
using System.Text.Json.Nodes;
using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Decks;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Units;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #12 — tái lập vòng phân căn ưu tiên (A2). Vòng đầu tiên cần danh mục căn, và là vòng đầu tiên
/// chứng minh được rằng <b>việc chọn căn nào vào quỹ ưu tiên</b> cũng mọc ra từ hạt giống chứ không
/// do người sắp: quỹ căn ưu tiên của từng loại được dựng lại trước, rồi mới tới chồng phiếu.
/// </summary>
public class TaiLapVongUuTienTests
{
    private const string VongUuTien2PN = "A2:2PN";
    private const string VongUuTien1PN = "A2:1PN";
    private const string VongQuyenMua = "A1";

    private static TransparencyReport Golden() => Parse(GoldenFixture.Json());

    private static TransparencyReport Parse(string json)
    {
        var result = TransparencyJson.Parse(json);
        Assert.True(result.Success, result.ErrorMessage);
        return result.Report!;
    }

    /// <summary>Danh mục căn của chính dự án trong fixture, nạp qua đúng đường người dùng nạp file.</summary>
    private static UnitCatalog DanhMuc()
    {
        var ketQua = UnitCatalogJson.Parse(GoldenFixture.UnitCatalogBytes());
        Assert.True(ketQua.Success, ketQua.ErrorMessage);
        return ketQua.Catalog!;
    }

    private static IReadOnlyList<CheckResult> TaiLapItems(TransparencyReport report, UnitCatalog? danhMuc) =>
        Verifier.Verify(new VerificationInput(report, danhMuc)).Items
            .Where(i => i.Id.StartsWith(CheckIds.DeckRebuild, StringComparison.Ordinal))
            .ToList();

    private static CheckResult TaiLap(TransparencyReport report, UnitCatalog? danhMuc, string vong) =>
        Assert.Single(TaiLapItems(report, danhMuc), i => i.Id == $"{CheckIds.DeckRebuild}:{vong}");

    private static DeckGrid Luoi(TransparencyReport report, UnitCatalog? danhMuc, string vong) =>
        Assert.Single(DeckGridBuilder.Build(report, danhMuc), l => l.Round == vong);

    private static string EditGolden(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(GoldenFixture.Json())!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    private static JsonObject ChongPhieu(JsonObject root, string vong) =>
        root["decks"]!.AsArray().First(d => d!["round"]!.GetValue<string>() == vong)!.AsObject();

    /// <summary>Đổi đúng MỘT byte hạt giống gốc vòng A — cả quỹ căn ưu tiên lẫn chồng phiếu phải lệch.</summary>
    private static TransparencyReport LechHatGiong() => Parse(EditGolden(root =>
    {
        var nguon = root["nguonNgauNhien"]!.AsArray().First(n => n!["round"]!.GetValue<string>() == "A")!.AsObject();
        var hex = nguon["masterSeed"]!.GetValue<string>();
        nguon["masterSeed"] = hex[..^2] + (Convert.ToByte(hex[^2..], 16) ^ 0x01).ToString("x2");
    }));

    /// <summary>Mã căn của các vé trúng đang công bố trong một chồng phiếu.</summary>
    private static IReadOnlyList<string> CanDaTrung(TransparencyReport report, string vong) =>
        report.Decks!.Single(d => d.Round == vong).Tickets!
            .Where(v => v!.StartsWith("TRUNG:", StringComparison.Ordinal))
            .Select(v => v!["TRUNG:".Length..])
            .ToList();

    private static UnitCatalog DanhMucCua(params (string Loai, string[] Can)[] loai) =>
        new("(danh mục dựng trong test)", loai.Select(l => new UnitCatalogType(l.Loai, l.Can)).ToList());

    // ── AC1: quỹ căn ưu tiên từng loại dựng lại từ hạt giống và hiện ra xem được ─────────

    /// <summary>
    /// Vector ghim: quỹ căn ưu tiên loại 2PN của fixture, theo đúng thứ tự hạt giống sinh ra. Con số
    /// này không nằm trong báo cáo minh bạch — nó ra từ <c>Shuffle(căn loại 2PN, "POOL:2PN")</c>.
    /// </summary>
    [Fact]
    public void AC1_QuyCanUuTien_DungLaiTuHatGiong_RaDungCacCanDaVaoChongPhieu()
    {
        var golden = Golden();
        var deck = golden.Decks!.Single(d => d.Round == VongUuTien2PN);

        var taiLap = DeckRebuilder.Rebuild(golden, deck, DanhMuc());

        Assert.NotNull(taiLap);
        Assert.Equal(["2PN-008", "2PN-007", "2PN-012", "2PN-001"], taiLap!.PoolUnits);
        Assert.Equal(
            CanDaTrung(golden, VongUuTien2PN).Order(StringComparer.Ordinal),
            taiLap.PoolUnits!.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AC1_QuyCanUuTien_HienRaXemDuoc_KemNhanVaHatGiongDanXuatCuaChinhNo()
    {
        var item = TaiLap(Golden(), DanhMuc(), VongUuTien2PN);

        var quy = Assert.Single(item.Metrics, m => m.Label.Contains("Quỹ căn ưu tiên", StringComparison.Ordinal));
        Assert.All(CanDaTrung(Golden(), VongUuTien2PN), can => Assert.Contains(can, quy.Value));

        Assert.Equal(
            LotteryLabels.PriorityPool("2PN"),
            Assert.Single(item.Metrics, m => m.Label.Contains("Nhãn dẫn xuất quỹ căn", StringComparison.Ordinal)).Value);
        Assert.Contains(item.Metrics, m => m.Label.Contains("Hạt giống dẫn xuất quỹ căn", StringComparison.Ordinal));
    }

    [Fact]
    public void AC1_DoiMotByteTrongHatGiong_QuyCanUuTienDoiTheo_HangMucChuyenKhongDat()
    {
        var lech = LechHatGiong();
        var deck = lech.Decks!.Single(d => d.Round == VongUuTien2PN);

        var taiLap = DeckRebuilder.Rebuild(lech, deck, DanhMuc());

        Assert.NotEqual(
            CanDaTrung(lech, VongUuTien2PN).Order(StringComparer.Ordinal),
            taiLap!.PoolUnits!.Order(StringComparer.Ordinal));
        Assert.Equal(CheckStatus.KhongDat, TaiLap(lech, DanhMuc(), VongUuTien2PN).Status);
    }

    /// <summary>
    /// Danh mục do người kiểm nạp vào có thể liệt căn theo thứ tự bất kỳ; phép xáo của backend sắp
    /// ordinal trước, nên bản dựng lại không được đổi theo thứ tự dòng trong file.
    /// </summary>
    [Fact]
    public void AC1_ThuTuCanTrongFileDanhMuc_KhongDoiQuyCanUuTienDungLai()
    {
        var golden = Golden();
        var deck = golden.Decks!.Single(d => d.Round == VongUuTien2PN);
        var daoNguoc = DanhMucCua(
            ("2PN", [.. Enumerable.Range(1, 12).Reverse().Select(i => $"2PN-{i:D3}")]),
            ("1PN", [.. Enumerable.Range(1, 8).Select(i => $"1PN-{i:D3}")]));

        var thuan = DeckRebuilder.Rebuild(golden, deck, DanhMuc());
        var dao = DeckRebuilder.Rebuild(golden, deck, daoNguoc);

        Assert.Equal(thuan!.PoolUnits, dao!.PoolUnits);
        Assert.Equal(thuan.DeckHash, dao.DeckHash);
    }

    // ── AC2: chồng phiếu từng loại dựng lại và so mã băm với bản công bố ─────────────────

    [Fact]
    public void AC2_MoiChongPhieuVongUuTien_DungLaiDuoc_VaKhopMaBamDaNiemPhong()
    {
        var golden = Golden();

        foreach (var vong in new[] { VongUuTien1PN, VongUuTien2PN })
        {
            var item = TaiLap(golden, DanhMuc(), vong);

            Assert.Equal(CheckStatus.Dat, item.Status);
            Assert.Equal(golden.Decks!.Single(d => d.Round == vong).DeckHash, item.Expected);
            Assert.Equal(item.Expected, item.Actual);
        }
    }

    [Fact]
    public void AC2_BanDungLai_RaDungTungLaVeDangCongBo_ChuKhongChiTrungMaBam()
    {
        var golden = Golden();
        var deck = golden.Decks!.Single(d => d.Round == VongUuTien2PN);

        var taiLap = DeckRebuilder.Rebuild(golden, deck, DanhMuc());

        Assert.Equal(deck.Tickets, taiLap!.Tickets);
    }

    [Fact]
    public void AC2_HangMuc_MangPreimageCuaBanDungLai_DeNguoiKiemTuBamLai()
    {
        var item = TaiLap(Golden(), DanhMuc(), VongUuTien2PN);

        Assert.False(string.IsNullOrWhiteSpace(item.Preimage));
        Assert.Equal(item.Actual, Hex.Sha256Hex(Encoding.UTF8.GetBytes(item.Preimage!)));
    }

    [Fact]
    public void AC2_VongCanDuVaSoDuKhuyet_VanChuaDungLai_KhongDeRaKetLuanBia()
    {
        var golden = Golden();

        Assert.All(
            golden.Decks!.Where(d => d.Round == "C"),
            d => Assert.Null(DeckRebuilder.Rebuild(golden, d, DanhMuc())));
    }

    // ── AC3: loại căn không có người đăng ký — xử lý đúng, không tạo kết luận giả ────────

    [Fact]
    public void AC3_LoaiCanKhongCoChongPhieuVongUuTien_CongCuKhongTuDeRaKetLuanChoNo()
    {
        var themLoaiRong = DanhMucCua(
            ("2PN", [.. Enumerable.Range(1, 12).Select(i => $"2PN-{i:D3}")]),
            ("1PN", [.. Enumerable.Range(1, 8).Select(i => $"1PN-{i:D3}")]),
            ("3PN", ["3PN-001", "3PN-002"]));

        var ids = TaiLapItems(Golden(), themLoaiRong).Select(i => i.Id).ToList();

        Assert.DoesNotContain($"{CheckIds.DeckRebuild}:A2:3PN", ids);
        Assert.DoesNotContain($"{CheckIds.DeckRebuild}:B:3PN", ids);
        Assert.Equal(
            [$"{CheckIds.DeckRebuild}:{VongQuyenMua}", $"{CheckIds.DeckRebuild}:{VongUuTien1PN}",
                $"{CheckIds.DeckRebuild}:{VongUuTien2PN}", $"{CheckIds.DeckRebuild}:B:1PN",
                $"{CheckIds.DeckRebuild}:B:2PN"],
            ids.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AC3_ChongPhieuVongUuTienRong_VanKetLuanDung_KhongNemNgoaiLe()
    {
        var rong = new TransparencyReport
        {
            EntropySources = Golden().EntropySources,
            Decks =
            [
                new Deck
                {
                    Round = "A2:3PN",
                    Size = 0,
                    WonCount = 0,
                    Tickets = [],
                    // SHA-256 của chuỗi rỗng: chồng phiếu 0 vé serialize ra 0 byte.
                    DeckHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                },
            ],
        };
        var danhMuc = DanhMucCua(("3PN", ["3PN-001", "3PN-002"]));

        var item = TaiLap(rong, danhMuc, "A2:3PN");

        Assert.Equal(CheckStatus.Dat, item.Status);
        Assert.Empty(Assert.Single(DeckGridBuilder.Build(rong, danhMuc)).Cells);
    }

    [Fact]
    public void AC3_ChongPhieuKhaiSoVeTrungNhieuHonSoCanCuaLoai_KhongKiemDuoc()
    {
        var golden = Golden();
        var thieuCan = DanhMucCua(
            ("2PN", ["2PN-001", "2PN-007"]),
            ("1PN", [.. Enumerable.Range(1, 8).Select(i => $"1PN-{i:D3}")]));

        var item = TaiLap(golden, thieuCan, VongUuTien2PN);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
    }

    // ── AC4: thiếu danh mục căn → KHÔNG KIỂM ĐƯỢC kèm hướng dẫn nạp danh mục ────────────

    [Fact]
    public void AC4_KhongCoDanhMucCan_KhongKiemDuoc_KemHuongDanNapDanhMuc()
    {
        var item = TaiLap(Golden(), danhMuc: null, VongUuTien2PN);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("danh mục căn", item.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nạp", item.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC4_DanhMucDangDungKhongCoLoaiCanNay_KhongKiemDuoc_ChuKhongKetToiGianLan()
    {
        // Danh mục nhúng sẵn là của dự án khác: kết luận KHÔNG ĐẠT ở đây là vu oan một buổi lễ sạch.
        var item = TaiLap(Golden(), EmbeddedUnitCatalog.Value, VongUuTien2PN);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("2PN", item.Explanation, StringComparison.Ordinal);
        Assert.Contains("nạp", item.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC4_DanhMucThieuCanDaCongBoTrongVe_KhongKiemDuoc_VaNoiRoCanNao()
    {
        var golden = Golden();
        var thieuMotCan = DanhMucCua(
            ("2PN", [.. Enumerable.Range(1, 12).Select(i => $"2PN-{i:D3}").Where(c => c != "2PN-012")]),
            ("1PN", [.. Enumerable.Range(1, 8).Select(i => $"1PN-{i:D3}")]));

        var item = TaiLap(golden, thieuMotCan, VongUuTien2PN);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("2PN-012", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC4_ThieuDanhMuc_KhongLamHongViecTaiLapVongQuyenMua()
    {
        var item = TaiLap(Golden(), danhMuc: null, VongQuyenMua);

        Assert.Equal(CheckStatus.Dat, item.Status);
    }

    // ── AC5: lưới ô phiếu đánh dấu ô khớp cho chồng phiếu vòng này ──────────────────────

    [Fact]
    public void AC5_LuoiVongUuTien_MoiODeuDanhDauKhopBanDungLai()
    {
        var golden = Golden();

        foreach (var vong in new[] { VongUuTien1PN, VongUuTien2PN })
        {
            var luoi = Luoi(golden, DanhMuc(), vong);

            Assert.NotEmpty(luoi.Cells);
            Assert.All(luoi.Cells, o => Assert.True(o.MatchesRebuild));
            Assert.Equal(luoi.Cells.Count, luoi.Summary.CellsMatchingRebuild);
        }
    }

    [Fact]
    public void AC5_TraoHaiVeTrongChongPhieuUuTien_ODoiChoLechHanBanDungLai()
    {
        var traoHaiVe = Parse(EditGolden(root =>
        {
            var ve = ChongPhieu(root, VongUuTien2PN)["tickets"]!.AsArray();
            var dau = ve[0]!.GetValue<string>();
            var i = Enumerable.Range(1, ve.Count - 1).First(k => ve[k]!.GetValue<string>() != dau);
            (ve[0], ve[i]) = (JsonValue.Create(ve[i]!.GetValue<string>()), JsonValue.Create(dau));
        }));

        var luoi = Luoi(traoHaiVe, DanhMuc(), VongUuTien2PN);

        Assert.Equal(2, luoi.Cells.Count(o => o.MatchesRebuild == false));
    }

    [Fact]
    public void AC5_KhongCoDanhMuc_LuoiVongUuTien_DeTrong_ChuKhongDanhDauLaLech()
    {
        var luoi = Luoi(Golden(), danhMuc: null, VongUuTien2PN);

        Assert.NotEmpty(luoi.Cells);
        Assert.All(luoi.Cells, o => Assert.Null(o.MatchesRebuild));
        Assert.Null(luoi.Summary.CellsMatchingRebuild);
    }
}
