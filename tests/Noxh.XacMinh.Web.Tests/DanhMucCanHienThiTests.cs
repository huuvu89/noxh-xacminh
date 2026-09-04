using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Noxh.XacMinh.Core.Units;
using Noxh.XacMinh.Web.Components;
using Noxh.XacMinh.Web.HienThi;
using Xunit;

namespace Noxh.XacMinh.Web.Tests;

/// <summary>
/// Vé #10 — màn hình danh mục căn hộ: hiện bản đang dùng, số liệu, mã băm, và nói thật về ranh giới
/// của nó. Test vẽ component thật để khẳng định thứ người kiểm đọc được, không phải model trung gian.
/// </summary>
public class DanhMucCanHienThiTests
{
    private const string DanhMucNho = """
        {
          "2PN-1WC": [
            { "unitCode": "2PN-1WC-D301" },
            { "unitCode": "2PN-1WC-D302" }
          ]
        }
        """;

    private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);

    private static Task<string> Ve(TrinhVe trinh) => trinh.Ve<DanhMucCan>([]);

    // ── AC1: danh mục nhúng sẵn, hiện tổng số căn và số căn theo từng loại ────────────────

    [Fact]
    public async Task AC1_HienTongSoCan_VaSoCanTungLoai()
    {
        await using var trinh = new TrinhVe();

        var html = await Ve(trinh);

        Assert.Contains("509", html);
        Assert.All(EmbeddedUnitCatalog.Value.Types, loai =>
        {
            Assert.Contains(loai.TypeCode, html);
            Assert.Contains(loai.UnitCodes.Count.ToString(), html);
        });
    }

    [Fact]
    public async Task AC1_HienSanKhiChuaNapBaoCaoNao()
    {
        await using var trinh = new TrinhVe();

        var html = await Ve(trinh);

        Assert.Contains("Danh mục căn hộ", html);
    }

    // ── AC2: hiện mã băm của bản danh mục đang dùng ───────────────────────────────────────

    [Fact]
    public async Task AC2_HienMaBamCuaBanDangDung()
    {
        await using var trinh = new TrinhVe();

        var html = await Ve(trinh);

        Assert.Contains("SHA-256", html);
        Assert.Contains(EmbeddedUnitCatalog.Value.Sha256, html);
    }

    // ── AC3: nạp file đè được, màn hình nói rõ đang dùng bản nào ──────────────────────────

    [Fact]
    public async Task AC3_MacDinh_NoiRoDangDungBanNhung()
    {
        await using var trinh = new TrinhVe();

        var html = await Ve(trinh);

        Assert.Equal(NguonDanhMuc.Nhung, trinh.DanhMuc.Nguon);
        Assert.Contains("bản nhúng sẵn", html);
        Assert.DoesNotContain("bản do bạn nạp", html);
    }

    [Fact]
    public async Task AC3_SauKhiNapFileKhac_NoiRoDangDungBanNguoiDungNap_KemTenFile()
    {
        await using var trinh = new TrinhVe();
        Assert.True(trinh.DanhMuc.Nap(Bytes(DanhMucNho), "danh-muc-cua-toi.json"));

        var html = await Ve(trinh);

        Assert.Contains("bản do bạn nạp", html);
        Assert.Contains("danh-muc-cua-toi.json", html);
    }

    [Fact]
    public async Task AC3_SauKhiNapFileKhac_SoLieuVaMaBamTheoFileMoi()
    {
        await using var trinh = new TrinhVe();
        trinh.DanhMuc.Nap(Bytes(DanhMucNho), "danh-muc-cua-toi.json");

        var html = await Ve(trinh);

        Assert.Contains(trinh.DanhMuc.DanhMuc.Sha256, html);
        Assert.DoesNotContain(EmbeddedUnitCatalog.Value.Sha256, html);
        Assert.NotEqual(EmbeddedUnitCatalog.Value.Total, trinh.DanhMuc.DanhMuc.Total);
    }

    [Fact]
    public async Task AC3_DungLaiBanNhung_QuayVeBanNhung()
    {
        await using var trinh = new TrinhVe();
        trinh.DanhMuc.Nap(Bytes(DanhMucNho), "danh-muc-cua-toi.json");

        trinh.DanhMuc.DungBanNhung();
        var html = await Ve(trinh);

        Assert.Equal(NguonDanhMuc.Nhung, trinh.DanhMuc.Nguon);
        Assert.Contains(EmbeddedUnitCatalog.Value.Sha256, html);
        Assert.Contains("bản nhúng sẵn", html);
    }

    [Fact]
    public void AC3_TrangThaiDanhMuc_SongTheoPhien_ChuKhongTheoLanNapFile()
    {
        var services = new ServiceCollection();
        services.ThemHienThi();

        var mota = Assert.Single(services, d => d.ServiceType == typeof(TrangThaiDanhMuc));

        // Scoped trong Blazor WebAssembly = một bản duy nhất cho cả phiên tab.
        Assert.Equal(ServiceLifetime.Scoped, mota.Lifetime);
    }

    // ── AC4: nói rõ danh mục là dữ liệu đầu vào công bố, không phải thứ công cụ chứng minh ─

    [Fact]
    public async Task AC4_CoCauGhiChuVeRanhGioiCuaDanhMuc()
    {
        await using var trinh = new TrinhVe();

        var html = await Ve(trinh);

        Assert.Contains("dữ liệu đầu vào do ban tổ chức công bố", html);
        Assert.Contains("nạp được file danh mục khác đè lên", html);
        // Câu phủ định "không phải thứ công cụ tự chứng minh" làm người dân hoang mang — đã bỏ.
        Assert.DoesNotContain("không phải thứ công cụ", html);
    }

    // ── AC5: file sai định dạng cho thông báo dễ hiểu, không phá trạng thái đang có ───────

    [Fact]
    public async Task AC5_NapFileHong_GiuNguyenDanhMucDangDung_VaBaoLoi()
    {
        await using var trinh = new TrinhVe();

        Assert.False(trinh.DanhMuc.Nap(Bytes("<html>404 Not Found</html>"), "trang-loi.html"));
        var html = await Ve(trinh);

        Assert.Equal(NguonDanhMuc.Nhung, trinh.DanhMuc.Nguon);
        Assert.Contains("509", html);
        Assert.Contains(EmbeddedUnitCatalog.Value.Sha256, html);
        Assert.Contains("JSON", html);
        Assert.Contains("Vẫn đang dùng bản nhúng sẵn", html);
    }

    [Fact]
    public async Task AC5_NapFileHongSauKhiDaNapBanRieng_VanGiuBanRiengDo()
    {
        await using var trinh = new TrinhVe();
        trinh.DanhMuc.Nap(Bytes(DanhMucNho), "danh-muc-cua-toi.json");

        Assert.False(trinh.DanhMuc.Nap(Bytes("{}"), "rong.json"));
        var html = await Ve(trinh);

        Assert.Equal(NguonDanhMuc.NguoiDungNap, trinh.DanhMuc.Nguon);
        Assert.Contains("Vẫn đang dùng bản do bạn nạp — danh-muc-cua-toi.json", html);
        Assert.Contains("loại căn", html);
    }

    [Fact]
    public async Task AC5_NapLaiFileTot_XoaThongBaoLoiCu()
    {
        await using var trinh = new TrinhVe();
        trinh.DanhMuc.Nap(Bytes("khong phai json"), "rac.txt");

        Assert.True(trinh.DanhMuc.Nap(Bytes(DanhMucNho), "danh-muc-cua-toi.json"));
        var html = await Ve(trinh);

        Assert.Null(trinh.DanhMuc.Loi);
        Assert.DoesNotContain("Vẫn đang dùng", html);
    }
}
