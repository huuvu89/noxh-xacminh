using Noxh.XacMinh.Core.Units;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Danh mục căn của <b>chính dự án trong fixture chuẩn vàng</b> — bản nhúng sẵn trong công cụ là của
/// dự án khác. Nạp qua đúng đường người kiểm nạp file, không dựng thẳng bằng tay: sai định dạng file
/// danh mục phải đỏ ở đây chứ không phải đỏ giữa buổi lễ.
/// </summary>
internal static class DanhMucGolden
{
    public static UnitCatalog Doc()
    {
        var ketQua = UnitCatalogJson.Parse(GoldenFixture.UnitCatalogBytes());
        Assert.True(ketQua.Success, ketQua.ErrorMessage);

        return ketQua.Catalog!;
    }
}
