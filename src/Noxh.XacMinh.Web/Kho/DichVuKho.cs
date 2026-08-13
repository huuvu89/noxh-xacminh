using Microsoft.Extensions.DependencyInjection;
using Noxh.XacMinh.Web.HienThi;

namespace Noxh.XacMinh.Web.Kho;

public static class DichVuKho
{
    /// <summary>Kho có thể trả về hàng nghìn lô; chờ lâu hơn thế thì coi như kho không đáp.</summary>
    private static readonly TimeSpan HanCho = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Đăng ký đường ra mạng thứ hai của công cụ: đọc kho bằng chứng. Tách khỏi <c>ThemHienThi</c>
    /// cùng lý do với tra cứu mốc neo — đọc code là thấy ngay chỗ nào gọi mạng.
    /// </summary>
    public static IServiceCollection ThemDocTrail(this IServiceCollection services) =>
        services
            .AddScoped(_ => new TaiKhoBangChung(new HttpClient { Timeout = HanCho }))
            .AddScoped(sp => new TrangThaiKho(sp.GetRequiredService<TaiKhoBangChung>().Doc));
}
