using Noxh.XacMinh.Core.Verification;

namespace Noxh.XacMinh.Web.HienThi;

/// <summary>Một chỗ duy nhất ánh xạ ba trạng thái sang lớp CSS — kết luận tổng và hạng mục phải cùng màu.</summary>
public static class LopTrangThai
{
    public static string Cua(CheckStatus status) => status switch
    {
        CheckStatus.Dat => "dat",
        CheckStatus.KhongDat => "khong-dat",
        _ => "khong-kiem-duoc",
    };
}
