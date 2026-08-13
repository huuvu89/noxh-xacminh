using Noxh.XacMinh.Web.BanDung;
using Noxh.XacMinh.Web.Components;
using Xunit;

namespace Noxh.XacMinh.Web.Tests;

/// <summary>
/// Vé #22 — AC2/AC3: chân trang là chỗ người hoài nghi đối chiếu "trang tôi đang mở có đúng là mã
/// nguồn công khai kia không". Nó phải in mã commit, mã băm gói và link lần chạy dựng, và khi
/// không có dấu vết nào thì phải nói thẳng chứ không bịa ra một dòng trông có vẻ đã kiểm.
/// </summary>
public class ChanTrangBanDungTests
{
    private const string Commit = "9f1c0a7d3b5e2846cf90a1b2c3d4e5f60718293a";

    private const string MaBamGoi = "3f2a91c4b7e8d0562a1b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8f";

    private const string LinkLanDung = "https://github.com/quydautu/noxh-xacminh/actions/runs/12345";

    private const string TenGoi = "noxh-xacminh-offline-9f1c0a7.zip";

    private static ThongTinBanDung BanCongKhai() => new(
        MaCommit: Commit,
        TenGoi: TenGoi,
        MaBamGoi: MaBamGoi,
        LinkLanDung: LinkLanDung,
        ThoiDiemDung: "2026-08-13T04:05:06Z");

    private static async Task<string> Ve(ThongTinBanDung thongTin)
    {
        await using var trinh = new TrinhVe();
        return await trinh.Ve<ChanTrangBanDung>(new Dictionary<string, object?>
        {
            ["ThongTin"] = thongTin,
        });
    }

    // ── AC2: chân trang in mã commit, mã băm gói và link tới lần chạy dựng ────────────────

    [Fact]
    public async Task AC2_ChanTrangInMaCommit_MaBamGoi_VaLinkToiLanChayDung()
    {
        var html = await Ve(BanCongKhai());

        Assert.Contains(Commit, html);
        Assert.Contains(MaBamGoi, html);
        Assert.Contains($"href=\"{LinkLanDung}\"", html);
    }

    [Fact]
    public async Task AC2_KhongCoDauVetBanDung_ThiNoiThang_KhongBia()
    {
        var html = await Ve(ThongTinBanDung.KhongRo);

        Assert.DoesNotContain("build-info", html);
        Assert.Contains("Chưa có dấu vết dựng công khai", html);

        // Không dấu vết mà vẫn hiện một link "lần chạy dựng" rỗng là mời người ta tin vào chỗ trống.
        Assert.DoesNotContain("href=\"\"", html);
    }

    // ── AC3: tải được bản offline ────────────────────────────────────────────────────────

    [Fact]
    public async Task AC3_CoNutTaiBanOffline_TroDungGoiDaDungCungLanDo()
    {
        var html = await Ve(BanCongKhai());

        Assert.Contains($"href=\"{TenGoi}\"", html);
        Assert.Contains("download", html);
    }

    [Fact]
    public async Task AC3_GoiOffline_KhongTuKhaiMaBamCuaChinhNo()
    {
        // Gói không thể chứa mã băm của chính nó, nên bản chạy offline in commit và link lần dựng,
        // rồi chỉ người kiểm sang chỗ có mã băm — im lặng bỏ trống là để họ tưởng đã đối chiếu xong.
        var trongGoi = BanCongKhai() with { MaBamGoi = "", TenGoi = "" };

        var html = await Ve(trongGoi);

        Assert.Contains(Commit, html);
        Assert.Contains(LinkLanDung, html);
        Assert.DoesNotContain("SHA-256</dt>", html);
        Assert.Contains("nhật ký lần dựng", html);
    }

    // ── Đọc dấu vết bản dựng ─────────────────────────────────────────────────────────────

    [Fact]
    public void AC2_DocDungCacTruongMaBuocDungGhiRa()
    {
        var json = $$"""
            {
              "maCommit": "{{Commit}}",
              "tenGoi": "{{TenGoi}}",
              "maBamGoi": "{{MaBamGoi}}",
              "linkLanDung": "{{LinkLanDung}}",
              "thoiDiemDung": "2026-08-13T04:05:06Z"
            }
            """;

        var thongTin = ThongTinBanDung.TuJson(json);

        Assert.NotNull(thongTin);
        Assert.Equal(Commit, thongTin!.MaCommit);
        Assert.Equal(TenGoi, thongTin.TenGoi);
        Assert.Equal(MaBamGoi, thongTin.MaBamGoi);
        Assert.Equal(LinkLanDung, thongTin.LinkLanDung);
    }

    [Theory]
    [InlineData("")]
    [InlineData("không phải json")]
    [InlineData("<!DOCTYPE html><html><body>404</body></html>")]
    public void AC2_DauVetDocKhongDuoc_ThiVeKhongRo_ChuKhongLamTrangTrang(string noiDung)
    {
        // Trang phục vụ tĩnh trả về HTML cho file thiếu là chuyện thường; một ngoại lệ ở đây làm
        // trắng cả công cụ kiểm chứng vì một dòng chân trang.
        Assert.Null(ThongTinBanDung.TuJson(noiDung));
    }
}
