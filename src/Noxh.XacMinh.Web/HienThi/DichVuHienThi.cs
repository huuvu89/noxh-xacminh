using Microsoft.Extensions.DependencyInjection;

namespace Noxh.XacMinh.Web.HienThi;

public static class DichVuHienThi
{
    /// <summary>Scoped trong Blazor WebAssembly = một bản duy nhất cho cả phiên tab.</summary>
    public static IServiceCollection ThemHienThi(this IServiceCollection services) =>
        services
            .AddScoped<TrangThaiHienThi>()
            .AddScoped<TrangThaiDanhMuc>();
}
