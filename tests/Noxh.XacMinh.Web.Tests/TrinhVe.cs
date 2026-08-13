using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Noxh.XacMinh.Web.HienThi;

namespace Noxh.XacMinh.Web.Tests;

/// <summary>
/// Vẽ component ra HTML tĩnh, dùng chung một <see cref="TrangThaiHienThi"/> cho cả phiên — đúng
/// như trong trình duyệt, nơi trạng thái chế độ sống lâu hơn từng lần nạp file.
/// </summary>
internal sealed class TrinhVe : IAsyncDisposable
{
    private readonly ServiceProvider dichVu;
    private readonly HtmlRenderer trinh;

    public TrinhVe(TrangThaiHienThi? trangThai = null, TrangThaiDanhMuc? danhMuc = null)
    {
        TrangThai = trangThai ?? new TrangThaiHienThi();
        DanhMuc = danhMuc ?? new TrangThaiDanhMuc();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TrangThai);
        services.AddSingleton(DanhMuc);
        services.AddSingleton<IJSRuntime, KhongGoiJs>();
        dichVu = services.BuildServiceProvider();
        trinh = new HtmlRenderer(dichVu, dichVu.GetRequiredService<ILoggerFactory>());
    }

    public TrangThaiHienThi TrangThai { get; }

    public TrangThaiDanhMuc DanhMuc { get; }

    /// <summary>
    /// Trả HTML đã giải mã thực thể: bộ vẽ escape mọi ký tự ngoài ASCII (<c>Đ</c> → <c>&amp;#x110;</c>),
    /// mà thứ cần khẳng định ở đây là chữ người dùng đọc được, không phải cách mã hoá.
    /// </summary>
    public Task<string> Ve<TComponent>(Dictionary<string, object?> thamSo)
        where TComponent : IComponent =>
        trinh.Dispatcher.InvokeAsync(async () =>
        {
            var ketQua = await trinh.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(thamSo));
            return WebUtility.HtmlDecode(ketQua.ToHtmlString());
        });

    public async ValueTask DisposeAsync()
    {
        await trinh.DisposeAsync();
        await dichVu.DisposeAsync();
    }

    /// <summary>
    /// <c>InputFile</c> đòi <see cref="IJSRuntime"/> ngay lúc dựng, nhưng vẽ HTML tĩnh thì không có
    /// trình duyệt để gọi — ném ra nếu ai đó thật sự gọi, thay vì giả vờ trả kết quả.
    /// </summary>
    private sealed class KhongGoiJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException($"Vẽ HTML tĩnh không gọi được JS ('{identifier}').");

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException($"Vẽ HTML tĩnh không gọi được JS ('{identifier}').");
    }
}
