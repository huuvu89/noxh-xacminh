using System.Text.Json;
using Noxh.XacMinh.Core.DanhSach;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #16 — AC7: test vector ghim cho phép băm danh sách, lấy từ backend.
///
/// Bảng ở <c>fixtures/danh-sach-golden.tsv</c> là danh sách hồ sơ mà generator seed vào backend
/// thật rồi khoá qua endpoint khoá danh sách, nên <c>listHash</c> đi kèm là con số backend tự sinh
/// ra — không phải con số công cụ này tự tính rồi tự khen. Hàng rào của điều đó là
/// <see cref="AC7_MaBamGhim_DungLaMaBamNamTrongDauThoiGianMocCamKet"/>: cùng một giá trị phải nằm
/// trong chuỗi đã được đóng dấu thời gian của fixture minh bạch.
/// </summary>
public class MaBamDanhSachTests
{
    private static byte[] Khoa() => KhoaChiMuc.Doc(GoldenFixture.DanhSachKhoaHex())!;

    private static IReadOnlyList<HoSoDanhSach> BangChuanVang()
    {
        var doc = BangDanhSach.Doc(GoldenFixture.DanhSach());
        Assert.Empty(doc.Loi);

        return doc.HoSo;
    }

    [Fact]
    public void AC7_BangDanhSachChuanVang_RaDungMaBamBackendDaGhim()
    {
        Assert.Equal(GoldenFixture.DanhSachListHash(), MaBamDanhSach.Tinh(BangChuanVang(), Khoa()));
    }

    [Fact]
    public void AC7_MaBamGhim_DungLaMaBamNamTrongDauThoiGianMocCamKet()
    {
        using var doc = JsonDocument.Parse(GoldenFixture.Json());
        var trongDau = doc.RootElement.GetProperty("dauThoiGian").EnumerateArray()
            .First(t => t.GetProperty("scope").GetString() == "FREEZE")
            .GetProperty("preimage").GetString()!
            .Split('\n')
            .First(d => d.StartsWith("listHash=", StringComparison.Ordinal))["listHash=".Length..];

        Assert.Equal(trongDau, GoldenFixture.DanhSachListHash());
    }

    /// <summary>
    /// Ba cái bẫy định dạng của <c>System.Text.Json</c> mặc định — sai một cái là công cụ báo KHÔNG
    /// ĐẠT cho một danh sách còn nguyên: tên trường viết hoa kiểu .NET, nhóm đối tượng ra <b>số</b>
    /// (không phải "U1"), và ký tự tiếng Việt bị escape <c>\uXXXX</c>.
    /// </summary>
    [Fact]
    public void AC7_ChuoiDemBam_TenTruongKieuDotNet_NhomRaSo_TiengVietBiEscape()
    {
        var chuoi = MaBamDanhSach.ChuoiDemBam(
            [new HoSoDanhSach("HS001", "Trần Thị Bình", "079010000001", 0)],
            Khoa());

        Assert.Equal(
            "[{\"MaHoSo\":\"HS001\","
            + "\"CccdBlindIndex\":\"cf361fef481f71e55f3da1f2cff2ef09c3641593425d045e83fed02f490115f3\","
            + "\"FullName\":\"Tr\\u1EA7n Th\\u1ECB B\\u00ECnh\",\"Group\":0}]",
            chuoi);
    }

    [Fact]
    public void ChiMucMu_KhongPhuThuocHoaThuongCuaSoDinhDanh()
    {
        Assert.Equal(
            MaBamDanhSach.ChiMucMu(Khoa(), "abc123"),
            MaBamDanhSach.ChiMucMu(Khoa(), "ABC123"));
    }

    /// <summary>
    /// Khoá 64 ký tự hex là khoá thật (32 byte); chuỗi khác là khoá dev đọc thẳng dạng UTF-8 rồi
    /// cắt/đệm về 32 byte — copy nguyên văn <c>EncryptionKeyProvider.ParseKey</c> của backend, vì
    /// chính bảng chuẩn vàng này được khoá bằng khoá dev.
    /// </summary>
    [Fact]
    public void KhoaChiMuc_DocDuocCaDangHexVaDangChuThuong()
    {
        Assert.Equal(
            KhoaChiMuc.Doc("DEV_IDX_KEY_REPLACE_IN_PROD_32B!"),
            KhoaChiMuc.Doc(GoldenFixture.DanhSachKhoaHex()));
    }

    [Fact]
    public void KhoaChiMuc_RongThiKhongCoKhoa()
    {
        Assert.Null(KhoaChiMuc.Doc("   "));
        Assert.Null(KhoaChiMuc.Doc(null));
    }
}
