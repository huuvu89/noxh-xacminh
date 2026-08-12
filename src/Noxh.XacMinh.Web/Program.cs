using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Noxh.XacMinh.Web;
using Noxh.XacMinh.Web.HienThi;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Lựa chọn chế độ hiển thị sống theo phiên, không theo lần nạp file.
builder.Services.ThemHienThi();

// Không đăng ký HttpClient: công cụ không gọi máy chủ nào cả — lập luận "kiểm chứng độc lập"
// chỉ mạnh khi nó không cần nói chuyện với hệ thống bị nghi ngờ.

await builder.Build().RunAsync();
