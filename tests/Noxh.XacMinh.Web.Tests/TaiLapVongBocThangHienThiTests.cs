using System.Text.RegularExpressions;
using Noxh.XacMinh.Core.Decks;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Units;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Noxh.XacMinh.Web.Components;
using Noxh.XacMinh.Web.HienThi;
using Xunit;

namespace Noxh.XacMinh.Web.Tests;

/// <summary>
/// Vé #13 — phần vẽ của vòng bốc thẳng theo loại căn: quỹ căn còn dư phải hiện ra xem được (AC1) và
/// <b>màn hình phải nói thẳng đó là dữ liệu suy diễn</b>, không phải dữ liệu ban tổ chức công bố
/// (AC3). Lưới đánh dấu ô khớp cho các chồng phiếu của vòng này (AC5).
/// </summary>
public class TaiLapVongBocThangHienThiTests
{
    private const string VongBocThang2PN = "B:2PN";

    private static TransparencyReport Golden()
    {
        var nap = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(nap.Success, nap.ErrorMessage);
        return nap.Report!;
    }

    private static UnitCatalog DanhMuc()
    {
        var ketQua = UnitCatalogJson.Parse(GoldenFixture.UnitCatalogBytes());
        Assert.True(ketQua.Success, ketQua.ErrorMessage);
        return ketQua.Catalog!;
    }

    private static DeckGrid Luoi(UnitCatalog? danhMuc, string vong) =>
        Assert.Single(DeckGridBuilder.Build(Golden(), danhMuc), l => l.Round == vong);

    private static Task<string> VeLuoi(TrinhVe trinh, UnitCatalog? danhMuc)
    {
        var luoi = Luoi(danhMuc, VongBocThang2PN);

        return trinh.Ve<LuoiOPhieu>(new Dictionary<string, object?>
        {
            ["Luoi"] = luoi,
            ["OHienThi"] = luoi.Cells,
        });
    }

    private static Task<string> VeKetQua(TrinhVe trinh, UnitCatalog? danhMuc) =>
        trinh.Ve<KetQuaKiem>(new Dictionary<string, object?>
        {
            ["BaoCao"] = Verifier.Verify(new VerificationInput(Golden(), danhMuc)),
            ["Nguon"] = "báo cáo.json",
        });

    // ── AC1: quỹ căn còn dư hiện ra xem được ────────────────────────────────────────────

    [Fact]
    public async Task AC1_Luoi_HienQuyCanConDuSuyRa_ChoNguoiDan_KhongChiOCheDoChuyenSau()
    {
        await using var trinh = new TrinhVe();

        var html = await VeLuoi(trinh, DanhMuc());

        Assert.Contains("Quỹ căn còn dư loại 2PN", html);
        foreach (var can in new[] { "2PN-002", "2PN-009", "2PN-011" })
            Assert.Contains(can, html);
    }

    [Fact]
    public async Task AC1_CheDoChuyenSau_HienNhanVaHatGiongDanXuatQuyCanConDu()
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(CheDoHienThi.ChuyenSau);

        var html = await VeKetQua(trinh, DanhMuc());

        Assert.Contains("B:units:2PN", html);
        Assert.Contains("Quỹ căn còn dư suy ra", html);
    }

    // ── AC3: màn hình nói rõ quỹ căn còn dư là dữ liệu suy diễn ─────────────────────────

    [Fact]
    public async Task AC3_Luoi_NoiRoQuyCanConDuLaSuyDien_KhongPhaiDuLieuCongBo()
    {
        await using var trinh = new TrinhVe();

        var html = await VeLuoi(trinh, DanhMuc());

        Assert.Contains("suy diễn", html);
        Assert.Contains("không phải dữ liệu ban tổ chức công bố", html);
    }

    [Fact]
    public async Task AC3_KhongCoDanhMuc_Luoi_KhongHienQuyCanConDuBiaRa()
    {
        await using var trinh = new TrinhVe();

        var html = await VeLuoi(trinh, danhMuc: null);

        Assert.DoesNotContain("Quỹ căn còn dư loại", html);
    }

    // ── AC5: lưới ô phiếu đánh dấu ô khớp cho chồng phiếu vòng này ──────────────────────

    [Fact]
    public async Task AC5_LuoiVongBocThang_MoiOMangDauHieuKhopBanDungLai()
    {
        await using var trinh = new TrinhVe();
        var soO = Golden().Decks!.Single(d => d.Round == VongBocThang2PN).Tickets!.Count;

        var html = await VeLuoi(trinh, DanhMuc());

        Assert.Equal(soO, Regex.Matches(html, " khop-tai-lap").Count);
        Assert.DoesNotContain(" lech-tai-lap", html);
    }
}
