using System.Text.RegularExpressions;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Pipeline.Tests;

/// <summary>
/// Hàng rào cho cấu hình pipeline (vé #21) — mỗi test mang tên một tiêu chí nghiệm thu.
/// Test đọc chính file <c>.gitlab-ci.yml</c> trong repo: GitLab mới là thứ chạy nó, nên ở đây
/// chỉ ghim những mệnh đề mà một lần sửa cẩu thả có thể làm mất im lặng (rules, allow_failure,
/// phạm vi lệnh test) — không mô phỏng lại GitLab.
/// </summary>
public class CauHinhPipelineTests
{
    private static readonly string Path_ = System.IO.Path.Combine(RepoPaths.RepoRoot, ".gitlab-ci.yml");

    private static string Yaml()
    {
        Assert.True(File.Exists(Path_), $"Thiếu cấu hình pipeline: {Path_}");
        return File.ReadAllText(Path_);
    }

    /// <summary>Cắt file thành các khối theo khoá cấp cao nhất (cột 0), đủ để soi từng job.</summary>
    private static Dictionary<string, string> TopLevelBlocks()
    {
        var blocks = new Dictionary<string, string>(StringComparer.Ordinal);
        string? key = null;
        var body = new List<string>();

        foreach (var line in Yaml().Split('\n'))
        {
            var m = Regex.Match(line, @"^([A-Za-z_][\w.\-]*):");
            if (m.Success)
            {
                if (key is not null) blocks[key] = string.Join('\n', body);
                key = m.Groups[1].Value;
                body = [line];
            }
            else
            {
                body.Add(line);
            }
        }

        if (key is not null) blocks[key] = string.Join('\n', body);
        return blocks;
    }

    private static Dictionary<string, string> Jobs() =>
        TopLevelBlocks()
            .Where(b => !b.Key.StartsWith('.') &&
                        b.Key is not ("stages" or "variables" or "workflow" or "default" or "include"))
            .ToDictionary(b => b.Key, b => b.Value, StringComparer.Ordinal);

    // ── AC1: pipeline chạy trên mọi lần đẩy lên nhánh chính và mọi merge request ───────────

    [Fact]
    public void AC1_PipelineChayTrenDayLenNhanhChinh_VaTrenMoiMergeRequest()
    {
        var blocks = TopLevelBlocks();
        Assert.True(blocks.ContainsKey("workflow"),
            "Thiếu workflow:rules — không khai báo được 'chạy trên nhánh chính và trên MR' ở một chỗ.");

        var workflow = blocks["workflow"];
        Assert.Contains("$CI_PIPELINE_SOURCE == \"merge_request_event\"", workflow);
        Assert.Matches(@"\$CI_COMMIT_BRANCH == \$CI_DEFAULT_BRANCH|\$CI_COMMIT_BRANCH == ""main""", workflow);
    }

    [Fact]
    public void AC1_KhongJobNaoTuChoiChayTrenMergeRequest()
    {
        // rules riêng của job có thể vô hiệu hoá workflow:rules một cách im lặng: pipeline vẫn
        // xanh trên MR chỉ vì chẳng job nào chạy.
        foreach (var (name, body) in Jobs())
        {
            Assert.False(Regex.IsMatch(body, @"only:|except:"),
                $"Job '{name}' dùng only/except đã lỗi thời — dùng rules để khỏi lệch với workflow:rules.");
            Assert.DoesNotContain("when: manual", body);
        }
    }

    // ── AC2: pipeline dựng dự án và chạy toàn bộ test ─────────────────────────────────────

    [Fact]
    public void AC2_CoJobChayToanBoTestCuaSolution()
    {
        var test = Assert.Single(Jobs().Values, j => j.Contains("dotnet test"));

        // Ghim đường dẫn project vào lệnh test là cách chắc chắn nhất để một project test mới
        // thêm vào solution không bao giờ được chạy trên CI.
        var lenh = Regex.Match(test, @"dotnet test[^\n]*").Value;
        Assert.DoesNotContain("tests/", lenh);
        Assert.DoesNotContain(".csproj", lenh);
    }

    [Fact]
    public void AC2_CoJobDungBanTinhCuaUngDung()
    {
        Assert.Contains(Jobs().Values,
            j => j.Contains("dotnet publish") && j.Contains("src/Noxh.XacMinh.Web"));
    }

    // ── AC3: test đỏ thì pipeline đỏ ──────────────────────────────────────────────────────

    [Fact]
    public void AC3_TestDoThiPipelineDo_KhongJobNaoDuocNuotMaLoi()
    {
        foreach (var (name, body) in Jobs())
        {
            Assert.DoesNotContain("allow_failure: true", body);
            Assert.False(Regex.IsMatch(body, @"(\|\||;)\s*true\s*$", RegexOptions.Multiline),
                $"Job '{name}' có bước nuốt mã lỗi ('|| true') — hỏng mà pipeline vẫn xanh.");
        }
    }

    // ── AC4: kết quả pipeline xem được từ giao diện GitLab ────────────────────────────────

    [Fact]
    public void AC4_KetQuaTestBaoCaoVeGiaoDienGitLab_KeCaKhiJobDo()
    {
        var test = Assert.Single(Jobs().Values, j => j.Contains("dotnet test"));
        Assert.Contains("junit", test);

        // Job đỏ mà không nộp báo cáo thì đúng lúc cần nhất lại không xem được test nào hỏng.
        Assert.Contains("when: always", test);
    }

    // ── AC5: thời gian chạy đủ ngắn để dùng được trong ngày làm việc ──────────────────────

    [Fact]
    public void AC5_MoiJobCoTranThoiGianKhongQuaMuoiLamPhut()
    {
        foreach (var (name, body) in Jobs())
        {
            var m = Regex.Match(body, @"^\s+timeout:\s*(\d+)\s*m\s*$", RegexOptions.Multiline);
            Assert.True(m.Success, $"Job '{name}' không khai báo timeout — treo thì treo tới trần của runner.");
            Assert.True(int.Parse(m.Groups[1].Value) <= 15,
                $"Job '{name}' đặt timeout > 15 phút, vượt trần 900s của runner nhóm.");
        }
    }
}
