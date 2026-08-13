using Noxh.XacMinh.Core.DanhSach;
using Noxh.XacMinh.TestSupport;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Bảng danh sách hồ sơ + khoá chỉ mục mù <b>đóng vai</b> thứ tổ giám sát dán vào công cụ. Test nào
/// khẳng định kết luận chung ĐẠT đều phải đưa cái này vào: thiếu nó thì hạng mục danh sách hồ sơ
/// đứng ở KHÔNG KIỂM ĐƯỢC và kéo kết luận chung theo, che mất thứ file đó đang kiểm.
/// </summary>
internal static class DanhSachGolden
{
    public static DanhSachDauVao Doc() =>
        new(GoldenFixture.DanhSach(), GoldenFixture.DanhSachKhoaHex());
}
