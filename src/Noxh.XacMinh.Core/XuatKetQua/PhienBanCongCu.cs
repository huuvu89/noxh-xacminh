using System.Reflection;

namespace Noxh.XacMinh.Core.XuatKetQua;

/// <summary>
/// Dấu vết nhận dạng bản công cụ đã ra kết luận. Số phiên bản một mình không đủ: hai người có thể
/// cầm hai bản dựng khác nhau của cùng số. MVID là định danh do trình biên dịch đóng vào assembly,
/// đổi theo từng lần dựng — hai bản xuất ghi cùng MVID nghĩa là cùng một binary đã chạy.
/// </summary>
public static class PhienBanCongCu
{
    public static string HienTai { get; } = Doc();

    private static string Doc()
    {
        var assembly = typeof(PhienBanCongCu).Assembly;

        var phienBan = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";

        var banDung = assembly.ManifestModule.ModuleVersionId.ToString("N")[..16];

        return $"noxh-xacminh {phienBan} (bản dựng {banDung})";
    }
}
