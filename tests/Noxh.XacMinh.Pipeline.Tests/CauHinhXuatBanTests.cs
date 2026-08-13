using System.Text.RegularExpressions;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Pipeline.Tests;

/// <summary>
/// Hàng rào cho việc xuất bản công khai (vé #22) — mỗi test mang tên một tiêu chí nghiệm thu.
/// Thứ thật sự dựng và đẩy trang là GitHub Actions, nên ở đây chỉ ghim những mệnh đề mà một lần
/// sửa cẩu thả có thể phá trong im lặng: dựng từ mã nguồn, dấu vết bản dựng đi kèm trang, gói
/// offline có thật, và đường dẫn con không rơi vào 404. Không mô phỏng lại GitHub Actions.
/// </summary>
public class CauHinhXuatBanTests
{
    private static readonly string DuongDanWorkflow =
        Path.Combine(RepoPaths.RepoRoot, ".github", "workflows", "xuat-ban.yml");

    private static readonly string DuongDanKichBan =
        Path.Combine(RepoPaths.RepoRoot, "deploy", "dung-ban-xuat-ban.sh");

    private static string Doc(string duongDan)
    {
        Assert.True(File.Exists(duongDan), $"Thiếu file: {duongDan}");
        return File.ReadAllText(duongDan);
    }

    private static string Workflow() => Doc(DuongDanWorkflow);

    private static string KichBan() => Doc(DuongDanKichBan);

    private static string IndexHtml() =>
        Doc(RepoPaths.Src("Noxh.XacMinh.Web", "wwwroot", "index.html"));

    // ── AC1: trang truy cập được công khai qua địa chỉ tĩnh, dùng được trên trình duyệt điện thoại ──

    [Fact]
    public void AC1_WorkflowDayBanTinhLenDiaChiTinhCongKhai()
    {
        var yml = Workflow();

        Assert.Contains("actions/upload-pages-artifact", yml);
        Assert.Contains("actions/deploy-pages", yml);

        // Thiếu một trong hai quyền này thì bước đẩy hỏng ngay, nhưng hỏng lúc chạy chứ không
        // lúc đọc — ghim ở đây để người sửa thấy trước.
        Assert.Matches(@"pages:\s*write", yml);
        Assert.Matches(@"id-token:\s*write", yml);
    }

    [Fact]
    public void AC1_TrangMoBangTrinhDuyetDienThoai_KhongPhuThuocNguonNgoai()
    {
        var html = IndexHtml();

        Assert.Contains("name=\"viewport\"", html);

        // Một link CDN lọt vào là trang vừa hỏng trên máy không có mạng, vừa mở thêm một bên thứ ba
        // có thể đổi mã đang chạy — hai thứ vé này phải chặn.
        Assert.DoesNotMatch(@"(src|href)=""https?://", html);
    }

    // ── AC2: chân trang in mã commit, mã băm gói và link tới lần chạy dựng ────────────────

    [Fact]
    public void AC2_BanDungGhiMaCommit_MaBamGoi_VaLinkLanChayDung_VaoTrang()
    {
        var kichBan = KichBan();

        // Trang đọc đúng file này để in chân trang; đổi tên trường ở một bên là chân trang trắng.
        Assert.Contains("build-info.json", kichBan);
        foreach (var truong in new[] { "maCommit", "maBamGoi", "linkLanDung", "tenGoi", "thoiDiemDung" })
            Assert.Contains($"\"{truong}\"", kichBan);

        Assert.Matches(@"sha256sum", kichBan);
    }

    [Fact]
    public void AC2_WorkflowDuaCommitThatVaLinkLanChayThat_VaoKichBan()
    {
        var yml = Workflow();

        Assert.Contains("COMMIT: ${{ github.sha }}", yml);
        Assert.Contains("github.run_id", yml);
    }

    // ── AC3: tải được bản offline, chạy được tại chỗ, không cần mạng ──────────────────────

    [Fact]
    public void AC3_KichBanDungGoiOffline_VaDatNgayTrenTrangDeTaiVe()
    {
        var kichBan = KichBan();

        Assert.Matches(@"noxh-xacminh-offline", kichBan);

        // Đóng gói bằng MSBuild, không bằng lệnh `zip`: image .NET SDK không cài sẵn `zip`, và một
        // bước dựng chỉ chạy được trên đúng một cái máy là bước dựng không ai kiểm lại được.
        Assert.Contains("DongGoiOffline.proj", kichBan);
        Assert.True(File.Exists(Path.Combine(RepoPaths.RepoRoot, "deploy", "DongGoiOffline.proj")));

        // Gói nằm ngoài trang thì nút tải trỏ vào hư không.
        Assert.Matches(@"cp .*TEN_GOI.*site", kichBan);
    }

    [Fact]
    public void AC3_GoiOfflineMangTheoHuongDanVaCachChayTaiCho()
    {
        var thuMuc = Path.Combine(RepoPaths.RepoRoot, "deploy", "offline");

        foreach (var ten in new[] { "HUONG-DAN-OFFLINE.md", "chay-offline.sh", "chay-offline.cmd" })
            Assert.True(File.Exists(Path.Combine(thuMuc, ten)), $"Thiếu {ten} trong gói offline.");

        Assert.Contains("deploy/offline", KichBan());
    }

    // ── AC4: việc dựng chạy tự động từ mã nguồn, không có bước dựng tay ───────────────────

    [Fact]
    public void AC4_MoiLanDayLenNhanhChinh_LaTuDungLaiTuMaNguon()
    {
        var yml = Workflow();

        Assert.Matches(@"on:\s*\n\s*push:\s*\n\s*branches:\s*\[\s*main\s*\]", yml.Replace("\r", ""));
        Assert.Contains("actions/checkout", yml);

        // Dựng phải nằm trong chính lần chạy này. Đẩy lên một artifact dựng sẵn ở đâu đó là mở
        // lại đúng khe hở mà vé này bịt: không ai đối chiếu được trang với mã nguồn nữa.
        Assert.Contains("dotnet publish", KichBan());
        Assert.Contains("deploy/dung-ban-xuat-ban.sh", yml);
    }

    [Fact]
    public void AC4_KhongJobNaoNuotMaLoi_HongThiPhaiDo()
    {
        var yml = Workflow();
        var kichBan = KichBan();

        Assert.DoesNotContain("continue-on-error: true", yml);
        Assert.False(Regex.IsMatch(yml, @"(\|\||;)\s*true\s*$", RegexOptions.Multiline),
            "Workflow có bước nuốt mã lỗi ('|| true') — hỏng mà lần dựng vẫn xanh.");

        // Kịch bản chạy nhiều lệnh nối nhau; thiếu `set -e` thì một bước hỏng ở giữa vẫn ra
        // thư mục xuất bản thiếu file, và trang lên sóng ở trạng thái đó.
        Assert.Matches(@"set -euo pipefail|set -eu", kichBan);
    }

    // ── AC5: đường dẫn con và tải lại trang không ra lỗi không tìm thấy ───────────────────

    [Fact]
    public void AC5_DuongDanConVaTaiLaiTrang_RoiVeChinhTrangKiemChung()
    {
        var kichBan = KichBan();

        // Máy chủ tĩnh không biết định tuyến của SPA: không có 404.html thì F5 ở đường dẫn con
        // ra thẳng trang lỗi của nhà cung cấp.
        Assert.Matches(@"404\.html", kichBan);
        Assert.Contains(".nojekyll", kichBan);
    }

    [Fact]
    public void AC5_BaseHrefDatTheoThuMucCuaDiaChiTinh_VaHongThiPhaiBaoLoi()
    {
        var kichBan = KichBan();
        var yml = Workflow();

        Assert.Contains("BASE_HREF", kichBan);
        Assert.Contains("base_path", yml);

        // Sửa index.html rồi mà phép thay không khớp nữa thì kịch bản phải đỏ, không được lặng lẽ
        // đẩy lên một trang mà _framework/ tải hụt.
        Assert.Contains("base href", kichBan);
        Assert.Matches(@"exit 1", kichBan);
    }

    [Fact]
    public void AC5_BaseHrefThieuDauGachCheoCuoi_DuocChuanHoa_ChuKhongDayLenNhuVay()
    {
        // `<base href="/noxh-xacminh">` làm `_framework/…` rơi về `/_framework/…` — trang đứng ở
        // màn hình "đang tải". Nhà cung cấp trả base_path không có dấu gạch chéo cuối là bình thường.
        Assert.Matches(@"case ""\$BASE_HREF"" in \*/\)", KichBan());
    }
}
