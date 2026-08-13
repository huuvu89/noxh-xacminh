using System.Text.RegularExpressions;
using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Decks;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Noxh.XacMinh.Web.Components;
using Noxh.XacMinh.Web.HienThi;
using Xunit;

namespace Noxh.XacMinh.Web.Tests;

/// <summary>
/// Vé #11 — phần vẽ của hạng mục tái lập: lưới đánh dấu ô khớp bản dựng lại (AC3) và chế độ chuyên
/// sâu hiện nhãn dẫn xuất hạt giống đã dùng (AC6).
/// </summary>
public class TaiLapChongPhieuHienThiTests
{
    private const string VongQuyenMua = "A1";

    private static TransparencyReport Golden()
    {
        var nap = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(nap.Success, nap.ErrorMessage);
        return nap.Report!;
    }

    private static DeckGrid Luoi(TransparencyReport baoCao, string vong) =>
        Assert.Single(DeckGridBuilder.Build(baoCao), l => l.Round == vong);

    private static Task<string> VeLuoi(TrinhVe trinh, DeckGrid luoi, int? viTriChon = null) =>
        trinh.Ve<LuoiOPhieu>(new Dictionary<string, object?>
        {
            ["Luoi"] = luoi,
            ["OHienThi"] = luoi.Cells,
            ["ViTriChon"] = viTriChon,
        });

    /// <summary>Đổi đúng MỘT byte hạt giống gốc vòng A — cả chồng phiếu dựng lại phải lệch.</summary>
    private static TransparencyReport LechHatGiong()
    {
        var golden = Golden();
        var nguon = golden.EntropySources!
            .Select(n => n.Round != "A"
                ? n
                : new EntropySource
                {
                    Round = n.Round,
                    MasterSeed = n.MasterSeed![..^2] + (Convert.ToByte(n.MasterSeed![^2..], 16) ^ 0x01).ToString("x2"),
                    RServer = n.RServer,
                    RServerCommit = n.RServerCommit,
                    RSupervisor = n.RSupervisor,
                    BlockHeight = n.BlockHeight,
                    BlockHash = n.BlockHash,
                    AnchorChain = n.AnchorChain,
                    InputHash = n.InputHash,
                })
            .ToList();

        return new TransparencyReport
        {
            EntropySources = nguon,
            Decks = golden.Decks,
            DrawLog = golden.DrawLog,
        };
    }

    // ── AC3: lưới đánh dấu ô khớp bản dựng lại ───────────────────────────────────────────

    [Fact]
    public async Task AC3_LuoiVongQuyenMua_MoiOMangDauHieuKhopBanDungLai()
    {
        await using var trinh = new TrinhVe();
        var luoi = Luoi(Golden(), VongQuyenMua);

        var html = await VeLuoi(trinh, luoi);

        Assert.Equal(luoi.Cells.Count, Regex.Matches(html, " khop-tai-lap").Count);
        Assert.DoesNotContain(" lech-tai-lap", html);
    }

    [Fact]
    public async Task AC3_OLechBanDungLai_MangDauHieuRieng_VaCoChuGiaiChu()
    {
        await using var trinh = new TrinhVe();
        var luoi = Luoi(LechHatGiong(), VongQuyenMua);

        var html = await VeLuoi(trinh, luoi);

        Assert.NotEmpty(Regex.Matches(html, " lech-tai-lap"));
        Assert.Contains("Lệch bản dựng lại", html);
        Assert.Contains("Khớp bản dựng lại", html);
    }

    [Fact]
    public async Task AC3_ChiTietOChon_NoiRoODoKhopHayLechBanDungLai()
    {
        await using var trinh = new TrinhVe();
        var luoi = Luoi(LechHatGiong(), VongQuyenMua);
        var oLech = luoi.Cells.First(o => o.MatchesRebuild == false);

        var html = await VeLuoi(trinh, luoi, viTriChon: oLech.Position);

        Assert.Contains("Dựng lại từ hạt giống", html);
    }

    [Fact]
    public async Task AC3_VongChuaDungLaiDuoc_NoiRoLaChuaDungLai_ChuKhongDeNguoiDocTuongDaKiem()
    {
        await using var trinh = new TrinhVe();
        var luoi = Luoi(Golden(), "C");

        var html = await VeLuoi(trinh, luoi);

        Assert.DoesNotContain(" khop-tai-lap", html);
        Assert.Contains("chưa dựng lại", html);
    }

    // ── AC6: chế độ chuyên sâu hiện nhãn dẫn xuất hạt giống đã dùng ──────────────────────

    [Fact]
    public async Task AC6_CheDoChuyenSau_HienNhanDanXuatHatGiongDaDung()
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(CheDoHienThi.ChuyenSau);

        var html = await trinh.Ve<KetQuaKiem>(new Dictionary<string, object?>
        {
            ["BaoCao"] = Verifier.Verify(new VerificationInput(Golden())),
            ["Nguon"] = "báo cáo.json",
        });

        Assert.Contains("Nhãn dẫn xuất hạt giống", html);
        Assert.Contains(LotteryLabels.A1Deck, html);
    }

    [Fact]
    public async Task AC6_CheDoNguoiDan_KhongHienNhanDanXuatHatGiong()
    {
        await using var trinh = new TrinhVe();

        var html = await trinh.Ve<KetQuaKiem>(new Dictionary<string, object?>
        {
            ["BaoCao"] = Verifier.Verify(new VerificationInput(Golden())),
            ["Nguon"] = "báo cáo.json",
        });

        Assert.DoesNotContain("Nhãn dẫn xuất hạt giống", html);
    }
}
