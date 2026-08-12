using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// AC7 — trang phải chạy được khi mở tĩnh (chỉ cần máy chủ file, không cần máy chủ ứng dụng).
/// Test giữ những điều kiện làm hỏng việc đó một cách âm thầm; việc dựng thật và mở bằng trình
/// duyệt vẫn nằm ở `dotnet publish` + phục vụ tĩnh, ghi trong README.
/// </summary>
public class StaticHostingTests
{
    private static string WebRoot(string file) =>
        RepoPaths.Src("Noxh.XacMinh.Web", "wwwroot", file);

    [Fact]
    public void AC7_TrangNap_BangRuntimeWasmTinh_KhongCanMayChuUngDung()
    {
        var html = File.ReadAllText(WebRoot("index.html"));

        Assert.Contains("_framework/blazor.webassembly.js", html);
        Assert.DoesNotContain("_framework/blazor.server.js", html);
        Assert.DoesNotContain("blazor.web.js", html);
    }

    [Fact]
    public void AC7_CoNojekyll_DeMayChuTinhKhongNuotThuMuc_framework()
    {
        Assert.True(File.Exists(WebRoot(".nojekyll")), "Thiếu .nojekyll — GitHub Pages sẽ bỏ qua _framework/");
    }
}
