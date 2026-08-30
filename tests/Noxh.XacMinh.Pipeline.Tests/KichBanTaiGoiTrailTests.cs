using System.Text.RegularExpressions;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Pipeline.Tests;

/// <summary>
/// Hàng rào cho script tải gói trail (<c>cong-cu/tai-goi-trail.py</c>). Thứ thật sự chạy script là
/// máy của tổ giám sát, nên ở đây chỉ ghim những mệnh đề mà một lần sửa cẩu thả có thể phá trong
/// im lặng — và mỗi mệnh đề đều có cái giá riêng nếu mất:
///  · <b>Khuôn gói khớp với lõi</b>: lệch tên thư mục là gói tải xong không nạp được, mà biết thì
///    đã ở giữa lễ.
///  · <b>Chỉ thư viện chuẩn</b>: máy tổ giám sát không cài thêm gì được, và mỗi phụ thuộc là một
///    thứ nữa phải tin.
///  · <b>Khoá không đi qua dòng lệnh</b>: dòng lệnh lộ ra ở <c>ps</c> và ở lịch sử shell.
///  · <b>Chỉ ký GET</b>: khoá dán nhầm có quyền ghi thì cũng không dựng nổi request sửa kho.
/// </summary>
public class KichBanTaiGoiTrailTests
{
    private static readonly string DuongDanScript =
        Path.Combine(RepoPaths.RepoRoot, "cong-cu", "tai-goi-trail.py");

    private static readonly string DuongDanLoi =
        RepoPaths.Src("Noxh.XacMinh.Core", "Kho", "DocGoiTrail.cs");

    private static string Doc(string duongDan)
    {
        Assert.True(File.Exists(duongDan), $"Thiếu file: {duongDan}");
        return File.ReadAllText(duongDan);
    }

    private static string Script() => Doc(DuongDanScript);

    /// <summary>Lấy giá trị một hằng chuỗi trong lõi — thứ script phải ghi ra cho đúng.</summary>
    private static string HangTrongLoi(string ten)
    {
        var khop = Regex.Match(Doc(DuongDanLoi), $@"const string {ten} = ""([^""]*)""");
        Assert.True(khop.Success, $"Không thấy hằng {ten} trong {DuongDanLoi}");

        return khop.Groups[1].Value;
    }

    [Theory]
    [InlineData("PhienBanChuan")]
    [InlineData("TenManifest")]
    [InlineData("ThuMucTrangDanhSach")]
    [InlineData("ThuMucNoiDung")]
    public void KhuonGoiKhopGiuaScriptVaLoi(string ten)
    {
        var giaTri = HangTrongLoi(ten);

        Assert.Contains($"\"{giaTri}\"", Script(), StringComparison.Ordinal);
    }

    [Fact]
    public void ChiDungThuVienChuanCuaPython()
    {
        string[] duocPhep =
        [
            "argparse", "concurrent.futures", "datetime", "getpass", "hashlib", "hmac", "io", "os",
            "sys", "time", "urllib.error", "urllib.parse", "urllib.request",
            "xml.etree.ElementTree", "zipfile",
        ];

        var nhap = Regex.Matches(Script(), @"^import ([\w.]+)", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.NotEmpty(nhap);
        Assert.All(nhap, m => Assert.Contains(m, duocPhep, StringComparer.Ordinal));
    }

    [Fact]
    public void KhoaKhongDiQuaThamSoDongLenh()
    {
        var script = Script();

        Assert.DoesNotContain("add_argument(\"--ma-khoa", script, StringComparison.Ordinal);
        Assert.DoesNotContain("add_argument(\"--bi-mat", script, StringComparison.Ordinal);
        Assert.Contains("NOXH_KHO_BI_MAT", script, StringComparison.Ordinal);
        Assert.Contains("getpass.getpass", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ChiKyVaChiPhatGet()
    {
        var script = Script();

        Assert.Contains("method=\"GET\"", script, StringComparison.Ordinal);
        foreach (var ghi in new[] { "\"PUT\"", "\"POST\"", "\"DELETE\"" })
            Assert.DoesNotContain(ghi, script, StringComparison.Ordinal);
    }

    /// <summary>
    /// Script tự kiểm được phần ký, offline, theo số AWS công bố — ký sai một byte thì kho từ chối,
    /// mà lúc đó đang giữa lễ. Ghim cả con chữ ký để không ai "sửa cho test xanh".
    /// </summary>
    [Fact]
    public void TuKiemChuKyTheoVectorChuanAws()
    {
        var script = Script();

        Assert.Contains("--tu-kiem", script, StringComparison.Ordinal);
        Assert.Contains(
            "fea454ca298b7da1c68078a5d1bdbfbbe0d65c699e0f91ac7a200a0136783543",
            script, StringComparison.Ordinal);
    }
}
