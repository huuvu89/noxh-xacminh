using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Pipeline.Tests;

/// <summary>
/// Vé #22 — chạy thật <c>deploy/dung-ban-xuat-ban.sh</c> trên một thư mục publish giả rồi soi thứ
/// nó đẻ ra. Đọc file cấu hình bằng regex bắt được cái tên bị đổi, không bắt được thứ tự bước sai
/// hay một phép thay không khớp; mà đây lại đúng là chỗ hỏng chỉ lộ ra khi trang đã lên sóng.
/// Dùng thư mục publish giả để test không phải trả giá một lần <c>dotnet publish</c>.
/// </summary>
public class DungBanXuatBanTests : IDisposable
{
    private const string Commit = "9f1c0a7d3b5e2846cf90a1b2c3d4e5f60718293a";

    private const string LinkLanDung = "https://example.invalid/actions/runs/12345";

    private const string ThoiDiem = "2026-08-13T04:05:06Z";

    private readonly string thuMuc = Path.Combine(
        Path.GetTempPath(), "noxh-xuat-ban-" + Guid.NewGuid().ToString("N")[..12]);

    private readonly string ra;

    private readonly string banTinh;

    public DungBanXuatBanTests()
    {
        ra = Path.Combine(thuMuc, "xuat-ban");
        banTinh = Path.Combine(thuMuc, "wwwroot");

        // Đúng những gì `dotnet publish` của Blazor WebAssembly đẻ ra và kịch bản đụng tới.
        Directory.CreateDirectory(Path.Combine(banTinh, "_framework"));
        Directory.CreateDirectory(Path.Combine(banTinh, "css"));
        File.WriteAllText(Path.Combine(banTinh, "index.html"),
            "<!DOCTYPE html>\n<html><head><base href=\"/\" />\n"
            + "<link rel=\"stylesheet\" href=\"css/app.css\" /></head>\n"
            + "<body><script src=\"_framework/blazor.webassembly.js\"></script></body></html>\n");
        File.WriteAllText(Path.Combine(banTinh, ".nojekyll"), "");
        File.WriteAllText(Path.Combine(banTinh, "css", "app.css"), "body{}\n");
        File.WriteAllText(Path.Combine(banTinh, "_framework", "blazor.webassembly.js"), "// runtime\n");
    }

    public void Dispose()
    {
        if (Directory.Exists(thuMuc)) Directory.Delete(thuMuc, recursive: true);
        GC.SuppressFinalize(this);
    }

    private void Chay(string baseHref)
    {
        var kichBan = Path.Combine(RepoPaths.RepoRoot, "deploy", "dung-ban-xuat-ban.sh");
        var tt = new ProcessStartInfo("bash", $"\"{kichBan}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = RepoPaths.RepoRoot,
        };
        tt.Environment["RA"] = ra;
        tt.Environment["BAN_TINH"] = banTinh;
        tt.Environment["BASE_HREF"] = baseHref;
        tt.Environment["COMMIT"] = Commit;
        tt.Environment["LINK_LAN_DUNG"] = LinkLanDung;
        tt.Environment["THOI_DIEM"] = ThoiDiem;

        using var tienTrinh = Process.Start(tt)!;
        var raChuan = tienTrinh.StandardOutput.ReadToEnd();
        var loiChuan = tienTrinh.StandardError.ReadToEnd();
        tienTrinh.WaitForExit();

        Assert.True(tienTrinh.ExitCode == 0,
            $"Kịch bản thoát mã {tienTrinh.ExitCode}.\n--- stdout ---\n{raChuan}\n--- stderr ---\n{loiChuan}");
    }

    private string Site(string duongDan) => Path.Combine(ra, "site", duongDan);

    private static JsonElement DauVet(string duongDan) =>
        JsonDocument.Parse(File.ReadAllText(duongDan)).RootElement;

    private string GoiOffline() =>
        Assert.Single(Directory.GetFiles(ra, "noxh-xacminh-offline-*.zip"));

    /// <summary>Bỏ tiền tố "./" nếu bộ nén thêm vào — nhưng giữ nguyên dấu chấm của `.nojekyll`.</summary>
    private static string TenTrongGoi(ZipArchiveEntry muc) =>
        muc.FullName.StartsWith("./", StringComparison.Ordinal) ? muc.FullName[2..] : muc.FullName;

    // ── AC2: chân trang in mã commit, mã băm gói và link tới lần chạy dựng ────────────────

    [Fact]
    public void AC2_DauVetBanDungMangDungCommit_MaBamGoiThat_VaLinkLanChayDung()
    {
        Chay("/noxh-xacminh/");

        var dauVet = DauVet(Site("build-info.json"));
        Assert.Equal(Commit, dauVet.GetProperty("maCommit").GetString());
        Assert.Equal(LinkLanDung, dauVet.GetProperty("linkLanDung").GetString());
        Assert.Equal(ThoiDiem, dauVet.GetProperty("thoiDiemDung").GetString());

        // Mã băm phải là mã băm của **đúng file người ta tải về**, không phải một con số ghi đại:
        // cả lập luận "trang này đúng là mã nguồn kia" treo trên chỗ này.
        var goi = Path.Combine(ra, "site", dauVet.GetProperty("tenGoi").GetString()!);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(goi))).ToLowerInvariant(),
            dauVet.GetProperty("maBamGoi").GetString());
    }

    [Fact]
    public void AC2_GoiOffline_KhongTuKhaiMaBamCuaChinhNo()
    {
        Chay("/noxh-xacminh/");

        using var goi = ZipFile.OpenRead(GoiOffline());
        var mucTieu = goi.GetEntry("./build-info.json") ?? goi.GetEntry("build-info.json");
        Assert.NotNull(mucTieu);

        using var doc = JsonDocument.Parse(new StreamReader(mucTieu!.Open()).ReadToEnd());
        Assert.Equal(Commit, doc.RootElement.GetProperty("maCommit").GetString());
        Assert.Equal("", doc.RootElement.GetProperty("maBamGoi").GetString());
        Assert.Equal("", doc.RootElement.GetProperty("tenGoi").GetString());
    }

    // ── AC3: tải được bản offline và bản đó chạy được tại chỗ, không cần mạng ─────────────

    [Fact]
    public void AC3_GoiOfflineNamNgayTrenTrang_VaMangDuBanCongCuKemHuongDan()
    {
        Chay("/noxh-xacminh/");

        var tenGoi = Path.GetFileName(GoiOffline());
        Assert.True(File.Exists(Site(tenGoi)), "Gói offline không nằm trên trang — nút tải trỏ vào hư không.");

        using var goi = ZipFile.OpenRead(GoiOffline());
        var ten = goi.Entries.Select(TenTrongGoi).ToHashSet();
        foreach (var can in new[]
                 {
                     "index.html", ".nojekyll", "css/app.css", "_framework/blazor.webassembly.js",
                     "HUONG-DAN-OFFLINE.md", "chay-offline.sh", "chay-offline.cmd",
                 })
            Assert.Contains(can, ten);
    }

    [Fact]
    public void AC3_GoiOffline_ChayODiaChiGoc_NenGiuBaseHrefRieng()
    {
        // Gói chạy sau một máy chủ file tại chỗ ở thư mục gốc; nhét base href của trang công khai
        // vào đây là gói tải về mở ra trắng trang.
        Chay("/noxh-xacminh/");

        using var goi = ZipFile.OpenRead(GoiOffline());
        var index = goi.Entries.Single(e => TenTrongGoi(e) == "index.html");
        Assert.Contains("<base href=\"/\"", new StreamReader(index.Open()).ReadToEnd());
    }

    // ── AC4: việc dựng chạy tự động từ mã nguồn, không có bước dựng tay ───────────────────

    [Fact]
    public void AC4_ThieuNojekyll_ThiDungLai_ChuKhongDayLenMotTrangMat_framework()
    {
        File.Delete(Path.Combine(banTinh, ".nojekyll"));

        var kichBan = Path.Combine(RepoPaths.RepoRoot, "deploy", "dung-ban-xuat-ban.sh");
        var tt = new ProcessStartInfo("bash", $"\"{kichBan}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = RepoPaths.RepoRoot,
        };
        tt.Environment["RA"] = ra;
        tt.Environment["BAN_TINH"] = banTinh;
        tt.Environment["COMMIT"] = Commit;

        using var tienTrinh = Process.Start(tt)!;
        tienTrinh.StandardOutput.ReadToEnd();
        tienTrinh.StandardError.ReadToEnd();
        tienTrinh.WaitForExit();

        Assert.NotEqual(0, tienTrinh.ExitCode);
    }

    // ── AC5: đường dẫn con và tải lại trang không ra lỗi không tìm thấy ───────────────────

    [Fact]
    public void AC5_TrangCo404HtmlLaBanSaoCuaTrangKiemChung()
    {
        Chay("/noxh-xacminh/");

        Assert.True(File.Exists(Site("404.html")));
        Assert.Equal(File.ReadAllText(Site("index.html")), File.ReadAllText(Site("404.html")));
    }

    [Theory]
    [InlineData("/noxh-xacminh/", "/noxh-xacminh/")]
    [InlineData("/noxh-xacminh", "/noxh-xacminh/")]
    [InlineData("", "/")]
    public void AC5_BaseHrefDatDungThuMucCuaDiaChiTinh_VaLuonCoDauGachCheoCuoi(
        string daoVao, string mongDoi)
    {
        Chay(daoVao);

        // Thiếu dấu gạch chéo cuối thì `_framework/…` rơi ra ngoài thư mục và trang đứng ở màn
        // hình "đang tải" — hỏng câm, chỉ thấy khi đã lên sóng.
        Assert.Contains($"<base href=\"{mongDoi}\"", File.ReadAllText(Site("index.html")));
        Assert.Contains($"<base href=\"{mongDoi}\"", File.ReadAllText(Site("404.html")));
    }
}
