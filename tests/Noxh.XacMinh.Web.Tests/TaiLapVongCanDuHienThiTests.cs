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
/// Vé #14 — phần vẽ của vòng căn dư: quỹ căn dư suy ra phải hiện ra kèm cảnh báo suy diễn, và màn
/// hình phải <b>giải thích được cho người dân rằng hạng dự khuyết không phụ thuộc thời điểm bấm</b>
/// (AC5). Số dự khuyết dựng lại hiện ngay trên lưới, không chỉ nằm ở chế độ chuyên sâu.
/// </summary>
public class TaiLapVongCanDuHienThiTests
{
    private const string VongCanDu = "C";

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

    private static Task<string> VeLuoi(TrinhVe trinh, UnitCatalog? danhMuc)
    {
        var luoi = Assert.Single(DeckGridBuilder.Build(Golden(), danhMuc), l => l.Round == VongCanDu);

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

    // ── AC5: giải thích hạng dự khuyết không phụ thuộc thời điểm bấm ────────────────────

    [Fact]
    public async Task AC5_Luoi_NoiThangHangDuKhuyetKhongPhuThuocThoiDiemBam()
    {
        await using var trinh = new TrinhVe();

        var html = await VeLuoi(trinh, DanhMuc());

        Assert.Contains("Số dự khuyết", html);
        Assert.Contains("bấm sớm", html);
        Assert.Contains("không phụ thuộc", html);
    }

    [Fact]
    public async Task AC5_Luoi_HienSoDuKhuyetDaDungLai_ChoNguoiDan_KhongChiOCheDoChuyenSau()
    {
        await using var trinh = new TrinhVe();

        var html = await VeLuoi(trinh, DanhMuc());

        Assert.Contains("3 · 2 · 1 · 4 · 5", html);
    }

    [Fact]
    public async Task AC5_KhongCoDanhMuc_Luoi_KhongHienSoDuKhuyetBiaRa()
    {
        await using var trinh = new TrinhVe();

        var html = await VeLuoi(trinh, danhMuc: null);

        Assert.DoesNotContain("3 · 2 · 1 · 4 · 5", html);
    }

    // ── Quỹ căn dư: vẫn là suy diễn, phải nói rõ ────────────────────────────────────────

    [Fact]
    public async Task Luoi_HienQuyCanDuSuyRa_KemCanhBaoDoLaDuLieuSuyDien()
    {
        await using var trinh = new TrinhVe();

        var html = await VeLuoi(trinh, DanhMuc());

        Assert.Contains("Quỹ căn dư", html);
        Assert.Contains("không phải dữ liệu ban tổ chức công bố", html);
        foreach (var can in new[] { "1PN-002", "1PN-006", "2PN-009", "2PN-011" })
            Assert.Contains(can, html);
    }

    [Fact]
    public async Task CheDoChuyenSau_HienNhanVaHatGiongDanXuatSoDuKhuyet()
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(CheDoHienThi.ChuyenSau);

        var html = await VeKetQua(trinh, DanhMuc());

        Assert.Contains("C:waitlist", html);
        Assert.Contains("C:units", html);
        Assert.Contains("Quy mô danh sách dự khuyết", html);
    }

    [Fact]
    public async Task LuoiVongCanDu_MoiOMangDauHieuKhopBanDungLai()
    {
        await using var trinh = new TrinhVe();
        var soO = Golden().Decks!.Single(d => d.Round == VongCanDu).Tickets!.Count;

        var html = await VeLuoi(trinh, DanhMuc());

        Assert.Equal(soO, Regex.Matches(html, " khop-tai-lap").Count);
        Assert.DoesNotContain(" lech-tai-lap", html);
    }
}
