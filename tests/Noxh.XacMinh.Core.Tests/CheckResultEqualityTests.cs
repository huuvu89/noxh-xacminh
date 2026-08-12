using Noxh.XacMinh.Core.Verification;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Hàng rào cho phép so sánh của <see cref="CheckResult"/>: kết quả kiểm hai lần trên cùng dữ liệu
/// phải bằng nhau (AC2 "dán == thả file") và khác nhau khi số liệu thật sự khác.
/// </summary>
public class CheckResultEqualityTests
{
    private static CheckResult Mau(params CheckMetric[] soLieu) =>
        new("id", "Tên", CheckStatus.Dat, "Giải thích", Expected: "a", Actual: "a", Preimage: "p")
        {
            Metrics = soLieu,
        };

    [Fact]
    public void HaiKetQua_CungNoiDung_KhacDanhSachSoLieu_VanBangNhau()
    {
        Assert.Equal(Mau(new CheckMetric("Số vé", "892")), Mau(new CheckMetric("Số vé", "892")));
    }

    [Fact]
    public void SoLieuThoKhacNhau_ThiKhongBangNhau()
    {
        Assert.NotEqual(Mau(new CheckMetric("Số vé", "892")), Mau(new CheckMetric("Số vé", "891")));
    }

    [Fact]
    public void MoiTruongCongKhai_DeuNamTrongPhepSoSanh()
    {
        // Equals viết tay (vì Metrics là danh sách): thêm trường mà quên sửa Equals thì test này đỏ.
        var truong = typeof(CheckResult).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(
            ["Actual", "Expected", "Explanation", "Id", "Metrics", "Preimage", "Status", "Title"],
            truong);
    }
}
