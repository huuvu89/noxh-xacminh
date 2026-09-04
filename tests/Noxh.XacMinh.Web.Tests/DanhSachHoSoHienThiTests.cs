using System.Reflection;
using Microsoft.AspNetCore.Components;
using Noxh.XacMinh.Core.DanhSach;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Noxh.XacMinh.Web.Components;
using Noxh.XacMinh.Web.HienThi;
using Xunit;

namespace Noxh.XacMinh.Web.Tests;

/// <summary>
/// Vé #16 — phần thuộc về vỏ giao diện: cảnh báo phải đến TRƯỚC ô dán khoá, và ở chế độ này công cụ
/// không được phát request nào cũng như không được ghi khoá ra bất kỳ nơi lưu trữ nào.
///
/// <see cref="TrinhVe"/> là hàng rào của vế sau: nó cấp một <c>IJSRuntime</c> ném ngoại lệ với mọi
/// lời gọi, mà <c>localStorage</c>/<c>sessionStorage</c>/cookie trong Blazor WebAssembly đều phải đi
/// qua JS — chỉ cần khung này lỡ chạm vào một nơi lưu trữ là test đỏ. Không có <c>HttpClient</c>
/// nào trong bộ dịch vụ, nên gọi mạng cũng vậy.
/// </summary>
public class DanhSachHoSoHienThiTests
{
    private static TransparencyReport ChuanVang()
    {
        var nap = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(nap.Success, nap.ErrorMessage);

        return nap.Report!;
    }

    private static Task<string> VeKhung(TrinhVe trinh) =>
        trinh.Ve<DanhSachHoSo>(new Dictionary<string, object?>());

    private static Task<string> VeKetQua(TrinhVe trinh, DanhSachDauVao? danhSach) =>
        trinh.Ve<KetQuaKiem>(new Dictionary<string, object?>
        {
            ["BaoCao"] = Verifier.Verify(new VerificationInput(ChuanVang(), DanhSach: danhSach)),
            ["Nguon"] = "báo cáo.json",
        });

    // ── AC3: cảnh báo về mức nhạy cảm của khoá, TRƯỚC khi người dùng dán ─────────────────

    [Fact]
    public async Task AC3_CanhBaoNhayCam_HienTruocOKhoa()
    {
        await using var trinh = new TrinhVe();

        var html = await VeKhung(trinh);

        Assert.Contains("Khoá chỉ mục mù là dữ liệu tối mật", html, StringComparison.Ordinal);
        Assert.Contains("không gửi đi bất kỳ request nào", html, StringComparison.Ordinal);
        // Chưa xác nhận đã đọc thì chưa có ô nào để dán khoá vào.
        Assert.DoesNotContain("type=\"password\"", html, StringComparison.Ordinal);
        Assert.Contains("Tôi đã đọc cảnh báo", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC3_XacNhanDaDocCanhBao_MoiMoODanBangVaKhoa()
    {
        var trangThai = new TrangThaiDanhSach();
        trangThai.HieuCanhBao();

        await using var trinh = new TrinhVe(danhSach: trangThai);

        var html = await VeKhung(trinh);

        Assert.Contains("type=\"password\"", html, StringComparison.Ordinal);
        Assert.Contains("mã hồ sơ", html, StringComparison.Ordinal);
        // Cảnh báo vẫn còn trên màn hình, không phải bấm xong là biến mất.
        Assert.Contains("Khoá chỉ mục mù là dữ liệu tối mật", html, StringComparison.Ordinal);
    }

    // ── Vé #17: thả thẳng file Excel gốc, đường dán giữ nguyên làm lối thoát ─────────────

    [Fact]
    public async Task AC1_KhungCoChoThaFileExcelGoc_NgayCanhOKhoa()
    {
        var trangThai = new TrangThaiDanhSach();
        trangThai.HieuCanhBao();

        await using var trinh = new TrinhVe(danhSach: trangThai);

        var html = await VeKhung(trinh);

        Assert.Contains("Thả thẳng file Excel gốc", html, StringComparison.Ordinal);
        Assert.Contains("accept=\".xlsx\"", html, StringComparison.Ordinal);
        Assert.Contains("tên tiêu đề", html, StringComparison.Ordinal);
    }

    /// <summary>AC5 — đường dán bảng vẫn còn nguyên trên màn hình, không bị file thay chỗ.</summary>
    [Fact]
    public async Task AC5_KhungVanConODanBang_LamLoiThoatKhiGapFileLa()
    {
        var trangThai = new TrangThaiDanhSach();
        trangThai.HieuCanhBao();

        await using var trinh = new TrinhVe(danhSach: trangThai);

        var html = await VeKhung(trinh);

        Assert.Contains("<textarea", html, StringComparison.Ordinal);
        Assert.Contains("dán bảng", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Vỏ đọc byte rồi đưa vào lõi, không tự diễn giải: file hỏng phải đi hết đường tới màn hình
    /// kết quả thành CHƯA ĐỦ DỮ LIỆU có nói tên file, chứ không thành ngoại lệ hay im lặng.
    /// </summary>
    [Fact]
    public async Task AC1_ThaFileExcelHong_ManHinhNoiKhongKiemDuocVaNoiTenFile()
    {
        await using var trinh = new TrinhVe();

        var html = await VeKetQua(trinh, new DanhSachDauVao(
            new NguonBang.Excel("danh-sach-goc.xlsx", "không phải file Excel"u8.ToArray()),
            GoldenFixture.DanhSachKhoaHex()));

        Assert.Contains("CHƯA ĐỦ DỮ LIỆU", html, StringComparison.Ordinal);
        Assert.Contains("danh-sach-goc.xlsx", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AC2_ByteCuaFileExcelCungChiNamTrongBoNho_VaXoaLaMatHan()
    {
        var trangThai = new TrangThaiDanhSach();
        trangThai.Dat(new NguonBang.Excel("danh-sach-goc.xlsx", [1, 2, 3]), GoldenFixture.DanhSachKhoaHex());

        Assert.IsType<NguonBang.Excel>(trangThai.DaDan!.Nguon);

        trangThai.Xoa();

        Assert.Null(trangThai.DaDan);
    }

    // ── AC2: khoá không rời khỏi bộ nhớ tab ─────────────────────────────────────────────

    [Fact]
    public async Task AC2_VeKhungVaDanKhoa_KhongChamToiNoiLuuTruNaoVaKhongGoiMang()
    {
        var trangThai = new TrangThaiDanhSach();
        trangThai.HieuCanhBao();
        trangThai.Dat(GoldenFixture.DanhSach(), GoldenFixture.DanhSachKhoaHex());

        await using var trinh = new TrinhVe(danhSach: trangThai);

        // Vẽ được là đã chứng minh: mọi lời gọi JS (đường duy nhất tới localStorage/cookie) và mọi
        // lời gọi mạng trong bộ dựng này đều ném ngoại lệ.
        var html = await VeKhung(trinh);

        Assert.Contains("Xoá bảng và khoá khỏi bộ nhớ", html, StringComparison.Ordinal);
        // Khoá không được vẽ ngược ra HTML — ảnh chụp màn hình cũng là một nơi rò rỉ.
        Assert.DoesNotContain(GoldenFixture.DanhSachKhoaHex(), html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Hàng rào chặt hơn một lần vẽ: khung dán danh sách chỉ được tiêm đúng <b>một</b> thứ — chỗ giữ
    /// bảng và khoá trong bộ nhớ. Không <c>HttpClient</c> thì không có đường ra mạng, không cầu nối
    /// JS thì không có đường tới <c>localStorage</c>/cookie. Ai thêm phụ thuộc mới vào khung này sẽ
    /// làm test đỏ và phải giải thích vì sao.
    /// </summary>
    [Fact]
    public void AC2_KhungDanhSach_ChiPhuThuocChoGiuBoNho_KhongMangKhongLuuTru()
    {
        var phuThuoc = typeof(DanhSachHoSo)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(p => p.IsDefined(typeof(InjectAttribute), inherit: true))
            .Select(p => p.PropertyType)
            .ToList();

        Assert.Equal(typeof(TrangThaiDanhSach), Assert.Single(phuThuoc));
        // Và chính chỗ giữ bộ nhớ đó cũng không có phụ thuộc nào để đi vòng ra ngoài.
        Assert.Empty(Assert.Single(typeof(TrangThaiDanhSach).GetConstructors()).GetParameters());
    }

    [Fact]
    public void AC2_XoaLaMatHan_VaChuaDanThiKhongCoGiTrongBoNho()
    {
        var trangThai = new TrangThaiDanhSach();
        Assert.Null(trangThai.DaDan);

        trangThai.Dat("HS001\tTrần Thị Bình\t079010000001\tU1", GoldenFixture.DanhSachKhoaHex());
        Assert.NotNull(trangThai.DaDan);

        trangThai.Xoa();

        Assert.Null(trangThai.DaDan);
    }

    // ── AC1/AC6: kết luận và bộ số hiện ra trên màn hình, không chỉ nằm trong lõi ────────

    [Fact]
    public async Task AC1_DanBangDungVaKhoaDung_ManHinhNoiDaKhopDanhSach()
    {
        await using var trinh = new TrinhVe();

        var html = await VeKetQua(trinh,
            new DanhSachDauVao(GoldenFixture.DanhSach(), GoldenFixture.DanhSachKhoaHex()));

        Assert.Contains("Danh sách hồ sơ đầu vào", html, StringComparison.Ordinal);
        Assert.Contains("ĐẠT", html, StringComparison.Ordinal);
        Assert.Contains("40 hồ sơ", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC1_ChuaDanGiCa_ManHinhNoiRoChuaKiemDuoc_ChuKhongLangLe()
    {
        await using var trinh = new TrinhVe();

        var html = await VeKetQua(trinh, null);

        Assert.Contains("Danh sách hồ sơ đầu vào", html, StringComparison.Ordinal);
        Assert.Contains("CHƯA ĐỦ DỮ LIỆU", html, StringComparison.Ordinal);
    }

    /// <summary>Họ tên trong bảng là dữ liệu cá nhân — kết luận không được bày nó ra màn hình.</summary>
    [Fact]
    public async Task KetQuaTrenManHinh_KhongBayHoTenCuaDanhSach()
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(CheDoHienThi.ChuyenSau);

        var html = await VeKetQua(trinh,
            new DanhSachDauVao(GoldenFixture.DanhSach(), GoldenFixture.DanhSachKhoaHex()));

        Assert.DoesNotContain("Vũ Hữu Mai", html, StringComparison.Ordinal);
    }
}
