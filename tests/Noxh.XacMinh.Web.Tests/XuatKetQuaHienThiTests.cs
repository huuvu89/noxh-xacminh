using System.Text;
using System.Text.RegularExpressions;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.Core.XuatKetQua;
using Noxh.XacMinh.TestSupport;
using Noxh.XacMinh.Web.Components;
using Noxh.XacMinh.Web.HienThi;
using Xunit;

namespace Noxh.XacMinh.Web.Tests;

/// <summary>
/// Vé #20 — AC1: xuất được file kết quả từ chính trang, không cần công cụ ngoài. Trang là WebAssembly
/// tĩnh nên đường tải phải tự chứa: link <c>data:</c> kèm <c>download</c>, không máy chủ, không JS.
/// </summary>
public class XuatKetQuaHienThiTests
{
    private const string HexDai = "3f2a91c4b7e8d0562a1b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8f";

    private static readonly DateTimeOffset LucKiem = new(2026, 8, 13, 3, 12, 45, TimeSpan.Zero);

    private static readonly Regex DuongDanDuLieu = new(
        "href=\"data:text/markdown;charset=utf-8;base64,([A-Za-z0-9+/=]+)\"", RegexOptions.Compiled);

    private static ThongTinBanXuat ThongTin() => new(
        LucKiem,
        PhienBanCongCu.HienTai,
        [new DauVaoDaKiem("Báo cáo minh bạch (bao-cao.json)", HexDai)],
        "Dự án Chung cư X — bao-cao.json");

    private static VerificationReport BaoCaoBaTrangThai() => new(
    [
        new CheckResult(
            "hang-muc-dat", "Mã băm chồng phiếu A1", CheckStatus.Dat,
            "Nội dung vé công bố khớp mã băm đã niêm phong.",
            Expected: HexDai, Actual: HexDai),
        new CheckResult(
            "hang-muc-khong-kiem-duoc", "Mã băm chồng phiếu C", CheckStatus.KhongKiemDuoc,
            "Chồng phiếu này chưa công bố nội dung vé, nên chưa kiểm được."),
    ]);

    private static Task<string> Ve(TrinhVe trinh, VerificationReport baoCao) =>
        trinh.Ve<KetQuaKiem>(new Dictionary<string, object?>
        {
            ["BaoCao"] = baoCao,
            ["Nguon"] = "bao-cao.json",
            ["ThongTinXuat"] = ThongTin(),
        });

    /// <summary>Nội dung file mà trình duyệt sẽ ghi ra đĩa khi bấm link — giải mã ngược từ HTML.</summary>
    private static string FileTaiVe(string html)
    {
        var khop = DuongDanDuLieu.Match(html);
        Assert.True(khop.Success, "Không tìm thấy link tải kết quả trong HTML.");

        return Encoding.UTF8.GetString(Convert.FromBase64String(khop.Groups[1].Value));
    }

    [Fact]
    public async Task AC1_TrangCoLinkTaiFile_KemTenFileGoiY()
    {
        await using var trinh = new TrinhVe();

        var html = await Ve(trinh, BaoCaoBaTrangThai());

        Assert.Contains($"download=\"{BanXuatVanBan.TenFile(ThongTin())}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC1_NoiDungTaiVe_DungBangBanXuatCuaLoi_KhongPhaiBanUiTuBia()
    {
        await using var trinh = new TrinhVe();
        var baoCao = BaoCaoBaTrangThai();

        var html = await Ve(trinh, baoCao);

        Assert.Equal(BanXuatVanBan.Dung(baoCao, ThongTin()), FileTaiVe(html));
    }

    [Theory]
    [InlineData(CheDoHienThi.NguoiDan)]
    [InlineData(CheDoHienThi.ChuyenSau)]
    public async Task AC1_FileXuatDayDu_DuMangHinhDangODangCheDoNao(CheDoHienThi cheDo)
    {
        // Chế độ người dân giấu hex trên màn hình; file thì không được giấu — nó là bản để trích dẫn.
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(cheDo);

        var noiDung = FileTaiVe(await Ve(trinh, BaoCaoBaTrangThai()));

        Assert.Contains(HexDai, noiDung, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC4_HangMucKhongKiemDuoc_ConNguyenTrongFileTaiVe()
    {
        await using var trinh = new TrinhVe();

        var noiDung = FileTaiVe(await Ve(trinh, BaoCaoBaTrangThai()));

        Assert.Contains("Mã băm chồng phiếu C — KHÔNG KIỂM ĐƯỢC", noiDung, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC1_ChuaNapBaoCao_ThiKhongCoLinkTaiChet()
    {
        await using var trinh = new TrinhVe();

        var html = await trinh.Ve<KetQuaKiem>(new Dictionary<string, object?>
        {
            ["BaoCao"] = BaoCaoBaTrangThai(),
            ["Nguon"] = "bao-cao.json",
        });

        Assert.DoesNotContain("download=", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AC1_TrangChu_LuonDuaThongTinXuatXuong_KeoNutTaiKhongBaoGioHien()
    {
        var home = File.ReadAllText(RepoPaths.Src("Noxh.XacMinh.Web", "Pages", "Home.razor"));

        Assert.Contains("ThongTinXuat=\"thongTinXuat\"", home, StringComparison.Ordinal);
    }

    [Fact]
    public void AC3_TrangChu_BamDungByteNguoiDungThaVao_DeDoiChieuBangSha256sum()
    {
        var home = File.ReadAllText(RepoPaths.Src("Noxh.XacMinh.Web", "Pages", "Home.razor"));

        Assert.Contains("Hex.Sha256Hex", home, StringComparison.Ordinal);
    }

    [Fact]
    public void AC1_KhongCanCongCuNgoai_TrangKhongThemFileJsNaoDeTaiVe()
    {
        var wwwroot = RepoPaths.Src("Noxh.XacMinh.Web", "wwwroot");

        Assert.Empty(Directory.GetFiles(wwwroot, "*.js", SearchOption.AllDirectories));
    }
}
