using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Noxh.XacMinh.Web;
using Noxh.XacMinh.Web.HienThi;
using Noxh.XacMinh.Web.MocNeo;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Lựa chọn chế độ hiển thị sống theo phiên, không theo lần nạp file.
builder.Services.ThemHienThi();

// Đường ra mạng duy nhất: đọc block ở độ cao đã cam kết từ sổ cái công khai, và chỉ khi người dùng
// bấm. Công cụ vẫn KHÔNG gọi máy chủ bốc thăm — lập luận "kiểm chứng độc lập" chỉ mạnh khi nó không
// cần nói chuyện với hệ thống bị nghi ngờ.
builder.Services.ThemTraCuuMocNeo();

await builder.Build().RunAsync();
