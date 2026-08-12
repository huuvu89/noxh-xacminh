using System.Text;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// AC2/AC3 — nạp dữ liệu: dán phải cho kết quả y hệt thả file, và JSON hỏng phải ra thông báo
/// dễ hiểu chứ không ném ngoại lệ (ngoại lệ lọt lên Blazor là trang trắng).
/// </summary>
public class TransparencyJsonTests
{
    // ── AC2: dán == thả file ─────────────────────────────────────────────────────────────

    [Fact]
    public void AC2_DanNoiDung_ChoKetQuaYHetThaFile()
    {
        // Thả file: trình duyệt đưa byte thô, giải mã UTF-8 rồi mới vào lõi.
        var thaFile = TransparencyJson.Parse(Encoding.UTF8.GetString(GoldenFixture.Bytes()));
        var dan = TransparencyJson.Parse(GoldenFixture.Json());

        Assert.True(thaFile.Success);
        Assert.True(dan.Success);

        var a = Verifier.Verify(new VerificationInput(thaFile.Report!));
        var b = Verifier.Verify(new VerificationInput(dan.Report!));

        Assert.Equal(a.Overall, b.Overall);
        Assert.Equal(a.Items, b.Items);
    }

    [Fact]
    public void AC2_NoiDungCoBOM_VanNapDuoc()
    {
        // Notepad/Excel hay ghi kèm BOM; người dán từ file như vậy không đáng bị báo lỗi.
        var result = TransparencyJson.Parse("﻿" + GoldenFixture.Json());

        Assert.True(result.Success, result.ErrorMessage);
    }

    [Fact]
    public void AC2_KhoangTrangThuaQuanhNoiDung_VanNapDuoc()
    {
        var result = TransparencyJson.Parse("\n  " + GoldenFixture.Json() + "  \n");

        Assert.True(result.Success, result.ErrorMessage);
    }

    // ── AC3: JSON hỏng → thông báo dễ hiểu, không ném ngoại lệ ───────────────────────────

    [Theory]
    [InlineData("{")]
    [InlineData("{\"decks\": [")]
    [InlineData("không phải json")]
    [InlineData("<html>404</html>")]
    [InlineData("[1, 2, 3]")]
    [InlineData("null")]
    public void AC3_JsonHong_TraLoiThongBao_KhongNemNgoaiLe(string rac)
    {
        var result = TransparencyJson.Parse(rac);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Null(result.Report);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n ")]
    [InlineData(null)]
    public void AC3_KhongCoNoiDung_TraLoiThongBaoRieng(string? rong)
    {
        var result = TransparencyJson.Parse(rong);

        Assert.False(result.Success);
        Assert.Contains("thả file", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC3_JsonHopLeNhungKhongPhaiBaoCaoMinhBach_NoiRoThieuKhoiNao()
    {
        var result = TransparencyJson.Parse("{\"hello\": \"world\"}");

        Assert.False(result.Success);
        Assert.Contains("chồng phiếu", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC3_JsonHong_ThongBaoChiRoViTriDeNguoiDungTuSoat()
    {
        var result = TransparencyJson.Parse("{\"decks\": [ }");

        Assert.Matches(@"dòng \d+", result.ErrorMessage!);
    }
}
