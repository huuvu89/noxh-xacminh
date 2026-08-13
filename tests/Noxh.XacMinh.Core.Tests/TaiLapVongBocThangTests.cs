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
/// Vé #13 — tái lập vòng bốc thẳng theo loại căn (B), vòng đông người nhất. Khác vòng phân căn ưu
/// tiên ở chỗ <b>quỹ căn còn dư không nằm trong báo cáo</b>: công cụ phải suy ra nó từ những căn đã
/// phân ở vòng trước. Suy diễn mâu thuẫn thì kết luận KHÔNG KIỂM ĐƯỢC — kết luận KHÔNG ĐẠT dựa trên
/// một suy diễn của chính công cụ là vu oan một buổi lễ sạch.
/// </summary>
public class TaiLapVongBocThangTests
{
    private const string VongBocThang2PN = "B:2PN";
    private const string VongBocThang1PN = "B:1PN";

    /// <summary>Quỹ căn còn dư loại 2PN của fixture: 12 căn trừ 4 căn đã phân ở vòng ưu tiên.</summary>
    private static readonly string[] QuyConDu2PN =
        ["2PN-002", "2PN-003", "2PN-004", "2PN-005", "2PN-006", "2PN-009", "2PN-010", "2PN-011"];

    /// <summary>Cùng quỹ đó sau khi xáo bằng hạt giống vòng B — thứ tự chảy vào chồng phiếu.</summary>
    private static readonly string[] QuyDaXao2PN =
        ["2PN-002", "2PN-009", "2PN-003", "2PN-011", "2PN-010", "2PN-004", "2PN-006", "2PN-005"];

    private static TransparencyReport Golden() => Parse(GoldenFixture.Json());

    private static TransparencyReport Parse(string json)
    {
        var result = TransparencyJson.Parse(json);
        Assert.True(result.Success, result.ErrorMessage);
        return result.Report!;
    }

    private static UnitCatalog DanhMuc()
    {
        var ketQua = UnitCatalogJson.Parse(GoldenFixture.UnitCatalogBytes());
        Assert.True(ketQua.Success, ketQua.ErrorMessage);
        return ketQua.Catalog!;
    }

    private static CheckResult TaiLap(TransparencyReport report, UnitCatalog? danhMuc, string vong) =>
        Assert.Single(
            Verifier.Verify(new VerificationInput(report, danhMuc)).Items,
            i => i.Id == $"{CheckIds.DeckRebuild}:{vong}");

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

    /// <summary>Dòng kết quả đầu tiên của một tầng — nơi sửa vào để dựng ca suy diễn mâu thuẫn.</summary>
    private static JsonObject DongKetQua(JsonObject root, string tang) =>
        root["ketQua"]!["rows"]!.AsArray().First(r => r!["tier"]!.GetValue<string>() == tang)!.AsObject();

    private static UnitCatalog DanhMucCua(params (string Loai, string[] Can)[] loai) =>
        new("(danh mục dựng trong test)", loai.Select(l => new UnitCatalogType(l.Loai, l.Can)).ToList());

    /// <summary>Mã căn của các vé trúng đang công bố trong một chồng phiếu.</summary>
    private static IReadOnlyList<string> CanDaTrung(TransparencyReport report, string vong) =>
        report.Decks!.Single(d => d.Round == vong).Tickets!
            .Where(v => v!.StartsWith("TRUNG:", StringComparison.Ordinal))
            .Select(v => v!["TRUNG:".Length..])
            .ToList();

    // ── AC1: quỹ căn còn dư suy ra từ kết quả vòng trước và hiện ra xem được ─────────────

    [Fact]
    public void AC1_QuyCanConDu_SuyRaTuCanDaPhanVongTruoc_RaDungCanChuaAiNhan()
    {
        var golden = Golden();
        var deck = golden.Decks!.Single(d => d.Round == VongBocThang2PN);

        var taiLap = DeckRebuilder.Rebuild(golden, deck, DanhMuc());

        Assert.NotNull(taiLap);
        Assert.Equal(QuyConDu2PN, taiLap!.LeftoverUnits);
        Assert.Equal(["2PN-001", "2PN-007", "2PN-008", "2PN-012"], taiLap.AllocatedBefore);
    }

    /// <summary>
    /// Vector ghim: quỹ căn còn dư sau khi xáo bằng <c>B:units:2PN</c> — con số này không nằm trong
    /// báo cáo, nó là thứ công cụ tự dựng lại rồi đem đối chiếu.
    /// </summary>
    [Fact]
    public void AC1_QuyCanConDu_XaoBangHatGiongVongB_RaDungCacCanDaVaoChongPhieu()
    {
        var golden = Golden();
        var deck = golden.Decks!.Single(d => d.Round == VongBocThang2PN);

        var taiLap = DeckRebuilder.Rebuild(golden, deck, DanhMuc());

        Assert.Equal(QuyDaXao2PN, taiLap!.PoolUnits);
        Assert.Equal(
            CanDaTrung(golden, VongBocThang2PN).Order(StringComparer.Ordinal),
            taiLap.PoolUnits!.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AC1_QuyCanConDu_HienRaXemDuoc_KemNhanVaHatGiongDanXuatCuaChinhNo()
    {
        var item = TaiLap(Golden(), DanhMuc(), VongBocThang2PN);

        var quy = Assert.Single(item.Metrics, m => m.Label.Contains("Quỹ căn còn dư suy ra", StringComparison.Ordinal));
        Assert.All(QuyConDu2PN, can => Assert.Contains(can, quy.Value));

        Assert.Equal(
            LotteryLabels.LeftoverUnits("2PN"),
            Assert.Single(item.Metrics, m => m.Label.Contains("Nhãn dẫn xuất quỹ căn còn dư", StringComparison.Ordinal))
                .Value);
        Assert.Contains(item.Metrics, m => m.Label.Contains("Hạt giống dẫn xuất quỹ căn còn dư", StringComparison.Ordinal));
        Assert.Contains(item.Metrics, m => m.Label.Contains("đã phân ở vòng trước", StringComparison.Ordinal));
    }

    // ── AC2: chồng phiếu từng loại dựng lại và so mã băm với bản công bố ─────────────────

    [Fact]
    public void AC2_MoiChongPhieuVongBocThang_DungLaiDuoc_VaKhopMaBamDaNiemPhong()
    {
        var golden = Golden();

        foreach (var vong in new[] { VongBocThang1PN, VongBocThang2PN })
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
        var deck = golden.Decks!.Single(d => d.Round == VongBocThang1PN);

        var taiLap = DeckRebuilder.Rebuild(golden, deck, DanhMuc());

        Assert.Equal(deck.Tickets, taiLap!.Tickets);
    }

    /// <summary>Đổi một byte hạt giống vòng B: chỉ vòng B lệch, vòng ưu tiên (hạt giống vòng A) không.</summary>
    [Fact]
    public void AC2_DoiMotByteHatGiongVongB_ChongPhieuVongBocThangLech_KetLuanKhongDat()
    {
        var lech = Parse(EditGolden(root =>
        {
            var nguon = root["nguonNgauNhien"]!.AsArray().First(n => n!["round"]!.GetValue<string>() == "B")!.AsObject();
            var hex = nguon["masterSeed"]!.GetValue<string>();
            nguon["masterSeed"] = hex[..^2] + (Convert.ToByte(hex[^2..], 16) ^ 0x01).ToString("x2");
        }));

        Assert.Equal(CheckStatus.KhongDat, TaiLap(lech, DanhMuc(), VongBocThang2PN).Status);
        Assert.Equal(CheckStatus.Dat, TaiLap(lech, DanhMuc(), "A2:2PN").Status);
    }

    /// <summary>
    /// Ca gian lận đáng sợ nhất: ban tổ chức tự sắp lại chồng phiếu rồi niêm phong chính bản sắp đặt,
    /// nên mã băm công bố khớp nội dung công bố. Quỹ căn không đổi ⇒ suy diễn vẫn nhất quán ⇒ công cụ
    /// phải kết luận thẳng KHÔNG ĐẠT, không được né sang KHÔNG KIỂM ĐƯỢC.
    /// </summary>
    [Fact]
    public void AC2_ChongPhieuSapLaiRoiNiemPhongBanSapDat_KetLuanKhongDat_ChuKhongNe()
    {
        var sapDat = Parse(EditGolden(root =>
        {
            var chongPhieu = ChongPhieu(root, VongBocThang2PN);
            var ve = chongPhieu["tickets"]!.AsArray();
            var dau = ve[0]!.GetValue<string>();
            var i = Enumerable.Range(1, ve.Count - 1).First(k => ve[k]!.GetValue<string>() != dau);
            (ve[0], ve[i]) = (JsonValue.Create(ve[i]!.GetValue<string>()), JsonValue.Create(dau));
            chongPhieu["deckHash"] = CanonicalDeckSerializer.Hash(
                ve.Select(v => v!.GetValue<string>()).ToList());
        }));

        var item = TaiLap(sapDat, DanhMuc(), VongBocThang2PN);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.NotEqual(item.Expected, item.Actual);
    }

    // ── AC3: nói rõ quỹ căn còn dư là dữ liệu suy diễn ───────────────────────────────────

    [Fact]
    public void AC3_HangMucDat_VanNoiRoQuyCanConDuLaSuyDien_KhongPhaiDuLieuCongBo()
    {
        var item = TaiLap(Golden(), DanhMuc(), VongBocThang2PN);

        Assert.Equal(CheckStatus.Dat, item.Status);
        Assert.Contains("suy ra", item.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("không phải", item.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC3_VongPhanCanUuTien_KhongBiDanNhamLaSuyDien()
    {
        var item = TaiLap(Golden(), DanhMuc(), "A2:2PN");

        Assert.DoesNotContain(item.Metrics, m => m.Label.Contains("suy ra", StringComparison.OrdinalIgnoreCase));
    }

    // ── AC4: suy diễn mâu thuẫn → KHÔNG KIỂM ĐƯỢC kèm nêu rõ mâu thuẫn ──────────────────

    [Fact]
    public void AC4_MotCanDuocPhanChoHaiHoSo_KhongKiemDuoc_VaNeuRoCanNao()
    {
        // Dòng tầng bốc xăm ưu tiên đổi sang căn mà một dòng khác đã nhận ⇒ căn phân hai lần.
        var phanHaiLan = Parse(EditGolden(root => DongKetQua(root, "UuTienBocXam")["unitCode"] = "2PN-001"));

        var item = TaiLap(phanHaiLan, DanhMuc(), VongBocThang2PN);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("2PN-001", item.Explanation, StringComparison.Ordinal);
        Assert.Contains("hai", item.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC4_CanDaPhanKhongCoTrongDanhMuc_KhongKiemDuoc_VaNeuRoCanNao()
    {
        var golden = Golden();
        var thieuMotCan = DanhMucCua(
            ("2PN", [.. Enumerable.Range(1, 12).Select(i => $"2PN-{i:D3}").Where(c => c != "2PN-008")]),
            ("1PN", [.. Enumerable.Range(1, 8).Select(i => $"1PN-{i:D3}")]));

        var item = TaiLap(golden, thieuMotCan, VongBocThang2PN);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("2PN-008", item.Explanation, StringComparison.Ordinal);
        Assert.Contains("danh mục", item.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Vé trúng công bố một căn mà suy diễn nói đã phân từ vòng trước: hoặc suy diễn sai, hoặc buổi
    /// lễ phân trùng. Công cụ không phân biệt được, nên không được kết luận KHÔNG ĐẠT.
    /// </summary>
    [Fact]
    public void AC4_VeTrungCongBoCanNamNgoaiQuyConDuSuyRa_KhongKiemDuoc_ChuKhongKhongDat()
    {
        var lechQuy = Parse(EditGolden(root =>
        {
            var ve = ChongPhieu(root, VongBocThang2PN)["tickets"]!.AsArray();
            var i = Enumerable.Range(0, ve.Count).First(k => ve[k]!.GetValue<string>().StartsWith("TRUNG:"));
            ve[i] = JsonValue.Create("TRUNG:2PN-001");
        }));

        var item = TaiLap(lechQuy, DanhMuc(), VongBocThang2PN);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("2PN-001", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC4_BaoCaoKhongCongBoBangKetQua_KhongKiemDuoc_VaNoiRoViSaoKhongSuyDuoc()
    {
        var khongKetQua = Parse(EditGolden(root => root.Remove("ketQua")));

        var item = TaiLap(khongKetQua, DanhMuc(), VongBocThang2PN);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("quỹ căn còn dư", item.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC4_TangKetQuaCongCuKhongBiet_KhongKiemDuoc_ChuKhongDoanBuaLaVongNao()
    {
        var tangLa = Parse(EditGolden(root => DongKetQua(root, "UuTienBocXam")["tier"] = "TangMoi"));

        var item = TaiLap(tangLa, DanhMuc(), VongBocThang2PN);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("TangMoi", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC4_SoVeTrungNhieuHonQuyCanConDu_KhongKiemDuoc()
    {
        var thuaVeTrung = Parse(EditGolden(root => ChongPhieu(root, VongBocThang2PN)["wonCount"] = 9));

        var item = TaiLap(thuaVeTrung, DanhMuc(), VongBocThang2PN);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
    }

    [Fact]
    public void AC4_KhongCoDanhMucCan_KhongKiemDuoc_KemHuongDanNapDanhMuc()
    {
        var item = TaiLap(Golden(), danhMuc: null, VongBocThang2PN);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("danh mục căn", item.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nạp", item.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    // ── AC5: lưới ô phiếu đánh dấu ô khớp cho các chồng phiếu của vòng này ───────────────

    [Fact]
    public void AC5_LuoiVongBocThang_MoiODeuDanhDauKhopBanDungLai()
    {
        var golden = Golden();

        foreach (var vong in new[] { VongBocThang1PN, VongBocThang2PN })
        {
            var luoi = Luoi(golden, DanhMuc(), vong);

            Assert.NotEmpty(luoi.Cells);
            Assert.All(luoi.Cells, o => Assert.True(o.MatchesRebuild));
            Assert.Equal(luoi.Cells.Count, luoi.Summary.CellsMatchingRebuild);
        }
    }

    [Fact]
    public void AC5_TraoHaiVeTrongChongPhieu_ODoiChoLechHanBanDungLai()
    {
        var traoHaiVe = Parse(EditGolden(root =>
        {
            var ve = ChongPhieu(root, VongBocThang1PN)["tickets"]!.AsArray();
            var dau = ve[0]!.GetValue<string>();
            var i = Enumerable.Range(1, ve.Count - 1).First(k => ve[k]!.GetValue<string>() != dau);
            (ve[0], ve[i]) = (JsonValue.Create(ve[i]!.GetValue<string>()), JsonValue.Create(dau));
        }));

        var luoi = Luoi(traoHaiVe, DanhMuc(), VongBocThang1PN);

        Assert.Equal(2, luoi.Cells.Count(o => o.MatchesRebuild == false));
    }

    [Fact]
    public void AC5_KhongCoDanhMuc_LuoiVongBocThang_DeTrong_ChuKhongDanhDauLaLech()
    {
        var luoi = Luoi(Golden(), danhMuc: null, VongBocThang2PN);

        Assert.NotEmpty(luoi.Cells);
        Assert.All(luoi.Cells, o => Assert.Null(o.MatchesRebuild));
        Assert.Null(luoi.Summary.CellsMatchingRebuild);
    }
}
