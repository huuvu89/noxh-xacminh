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
/// Vé #12 — phần vẽ của vòng phân căn ưu tiên: quỹ căn ưu tiên dựng lại phải <b>hiện ra xem được</b>
/// (AC1), lưới đánh dấu ô khớp cho chồng phiếu vòng này (AC5), và thiếu danh mục thì nói rõ là chưa
/// kiểm được kèm hướng dẫn nạp (AC4) chứ không im lặng.
/// </summary>
public class TaiLapVongUuTienHienThiTests
{
    private const string VongUuTien2PN = "A2:2PN";

    private static TransparencyReport Golden()
    {
        var nap = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(nap.Success, nap.ErrorMessage);
        return nap.Report!;
    }

    /// <summary>Danh mục căn của chính dự án trong fixture — bản nhúng sẵn là của dự án khác.</summary>
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
        var luoi = Luoi(danhMuc, VongUuTien2PN);

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

    // ── AC1: quỹ căn ưu tiên dựng lại hiện ra xem được ──────────────────────────────────

    [Fact]
    public async Task AC1_Luoi_HienQuyCanUuTienDungLai_ChoNguoiDan_KhongChiOCheDoChuyenSau()
    {
        await using var trinh = new TrinhVe();

        var html = await VeLuoi(trinh, DanhMuc());

        Assert.Contains("Quỹ căn ưu tiên loại 2PN", html);
        foreach (var can in new[] { "2PN-008", "2PN-007", "2PN-012", "2PN-001" })
            Assert.Contains(can, html);
    }

    [Fact]
    public async Task AC1_CheDoChuyenSau_HienNhanVaHatGiongDanXuatQuyCanUuTien()
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(CheDoHienThi.ChuyenSau);

        var html = await VeKetQua(trinh, DanhMuc());

        Assert.Contains("POOL:2PN", html);
        Assert.Contains("Quỹ căn ưu tiên dựng lại từ hạt giống", html);
    }

    [Fact]
    public async Task AC1_KhongCoDanhMuc_Luoi_KhongHienQuyCanUuTienBiaRa()
    {
        await using var trinh = new TrinhVe();

        var html = await VeLuoi(trinh, danhMuc: null);

        Assert.DoesNotContain("Quỹ căn ưu tiên loại", html);
    }

    // ── AC4: thiếu danh mục → nói rõ chưa kiểm được, kèm hướng dẫn nạp ──────────────────

    [Fact]
    public async Task AC4_KhongCoDanhMuc_NoiRoChuaKiemDuoc_KemHuongDanNapDanhMuc()
    {
        await using var trinh = new TrinhVe();

        var html = await VeKetQua(trinh, danhMuc: null);

        Assert.Contains("Tái lập vòng phân căn ưu tiên", html);
        Assert.Contains("nạp file danh mục căn", html);
    }

    // ── AC5: lưới ô phiếu đánh dấu ô khớp cho chồng phiếu vòng này ──────────────────────

    [Fact]
    public async Task AC5_LuoiVongUuTien_MoiOMangDauHieuKhopBanDungLai()
    {
        await using var trinh = new TrinhVe();
        var soO = Golden().Decks!.Single(d => d.Round == VongUuTien2PN).Tickets!.Count;

        var html = await VeLuoi(trinh, DanhMuc());

        Assert.Equal(soO, Regex.Matches(html, " khop-tai-lap").Count);
        Assert.DoesNotContain(" lech-tai-lap", html);
    }

    [Fact]
    public async Task AC5_KhongCoDanhMuc_LuoiVongUuTien_NoiRoLaChuaDungLai()
    {
        await using var trinh = new TrinhVe();

        var html = await VeLuoi(trinh, danhMuc: null);

        Assert.DoesNotContain(" khop-tai-lap", html);
        Assert.Contains("chưa dựng lại", html);
    }
}
