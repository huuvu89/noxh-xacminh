using Microsoft.Extensions.DependencyInjection;
using Noxh.XacMinh.Web.HienThi;

namespace Noxh.XacMinh.Web.MocNeo;

public static class DichVuMocNeo
{
    /// <summary>Chờ lâu hơn thế thì người kiểm tự mở link nhanh hơn — và trang không được treo vì một nguồn im.</summary>
    private static readonly TimeSpan HanCho = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Đăng ký đường ra mạng <b>duy nhất</b> của công cụ. Tách khỏi <c>ThemHienThi</c> để đọc code là
    /// thấy ngay chỗ nào gọi mạng: mọi thứ còn lại chạy trọn trong máy người dùng.
    /// </summary>
    public static IServiceCollection ThemTraCuuMocNeo(this IServiceCollection services) =>
        services
            .AddScoped(_ => new HttpClient { Timeout = HanCho })
            .AddScoped<TraCuuKhoiCongKhai>()
            .AddScoped(sp => new TrangThaiMocNeo(sp.GetRequiredService<TraCuuKhoiCongKhai>().Doc));
}
