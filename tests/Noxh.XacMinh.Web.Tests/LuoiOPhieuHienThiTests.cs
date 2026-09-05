using System.Diagnostics;
using System.Text.RegularExpressions;
using Noxh.XacMinh.Core.Decks;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.TestSupport;
using Noxh.XacMinh.Web.Components;
using Noxh.XacMinh.Web.HienThi;
using Xunit;

namespace Noxh.XacMinh.Web.Tests;

/// <summary>
/// Vé #9 — lưới ô phiếu vẽ ra HTML. Trạng thái (chồng phiếu đang xem, bộ lọc, ô đang chọn) nằm ở
/// <see cref="KhungLuoiPhieu"/>, còn <see cref="LuoiOPhieu"/> chỉ vẽ, nên test đưa thẳng trạng thái
/// vào và khẳng định trên HTML thật — không cần trình duyệt.
/// </summary>
public class LuoiOPhieuHienThiTests
{
    private const string VongC = "C";

    private static readonly Regex OPhieu = new("class=\"o-phieu", RegexOptions.Compiled);

    private static readonly Regex KhoiO = new("class=\"khoi-o\"", RegexOptions.Compiled);

    private static TransparencyReport Golden()
    {
        var nap = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(nap.Success, nap.ErrorMessage);
        return nap.Report!;
    }

    private static DeckGrid Luoi(string vong) =>
        Assert.Single(DeckGridBuilder.Build(Golden()), l => l.Round == vong);

    private static Task<string> VeLuoi(
        TrinhVe trinh,
        DeckGrid luoi,
        IReadOnlyList<DeckCell>? hienThi = null,
        int? viTriChon = null) =>
        trinh.Ve<LuoiOPhieu>(new Dictionary<string, object?>
        {
            ["Luoi"] = luoi,
            ["OHienThi"] = hienThi ?? luoi.Cells,
            ["ViTriChon"] = viTriChon,
        });

    private static Task<string> VeKhung(TrinhVe trinh, TransparencyReport baoCao) =>
        trinh.Ve<KhungLuoiPhieu>(new Dictionary<string, object?> { ["BaoCao"] = baoCao });

    // ── AC1: chọn được chồng phiếu, mỗi vé là một ô có màu theo loại kết quả ─────────────

    [Fact]
    public async Task AC1_KhungLuoi_ChoChonTungChongPhieuDaCongBo()
    {
        await using var trinh = new TrinhVe();

        var html = await VeKhung(trinh, Golden());

        Assert.Contains("chon-chong-phieu", html);
        Assert.All(Golden().Decks!, d => Assert.Contains(d.Round!, html));
    }

    [Fact]
    public async Task AC1_MoiVeLaMotO_DuSoOCuaChongPhieu()
    {
        await using var trinh = new TrinhVe();
        var luoi = Luoi(VongC);

        var html = await VeLuoi(trinh, luoi);

        Assert.Equal(luoi.Cells.Count, OPhieu.Matches(html).Count);
    }

    [Fact]
    public async Task AC1_OMangLopMauTheoLoaiKetQua()
    {
        await using var trinh = new TrinhVe();
        var luoi = Luoi(VongC);

        var html = await VeLuoi(trinh, luoi);

        foreach (var loai in luoi.Cells.Select(o => o.Kind).Distinct())
            Assert.Contains(LopLoaiVe.Cua(loai), html);
    }

    [Fact]
    public async Task AC1_CoChuGiaiChuChoTungMau_KeoMuMauKhongDocDuoc()
    {
        await using var trinh = new TrinhVe();
        var luoi = Luoi(VongC);

        var html = await VeLuoi(trinh, luoi);

        foreach (var loai in luoi.Cells.Select(o => o.Kind).Distinct())
            Assert.Contains(LopLoaiVe.Nhan(loai), html);
    }

    [Fact]
    public async Task AC1_ChongPhieuChuaCongBoNoiDungVe_NoiRoLaChuaCoGiDeVe()
    {
        await using var trinh = new TrinhVe();
        var chuaMo = Assert.Single(DeckGridBuilder.Build(
            new TransparencyReport { Decks = [new Deck { Round = "C", Size = 21 }] }));

        var html = await VeLuoi(trinh, chuaMo);

        Assert.Empty(OPhieu.Matches(html));
        Assert.Contains("chưa công bố nội dung vé", html);
    }

    // ── AC2: chạm/rê vào ô hiện vị trí, nội dung vé, mã định danh hồ sơ, người bấm hay máy bốc ─

    [Fact]
    public async Task AC2_MoiO_CoMoTaKemViTriVaNoiDungVe()
    {
        await using var trinh = new TrinhVe();
        var luoi = Luoi(VongC);
        var o = luoi.Cells.First(x => x.Drawn);

        var html = await VeLuoi(trinh, luoi);

        Assert.Contains($"Vị trí {o.Position}", html);
        Assert.Contains(o.Payload!, html);
    }

    [Fact]
    public async Task AC2_OMayBocThay_CoNhanMayBoc_ODaBocConLaiCoNhanNguoiBam()
    {
        await using var trinh = new TrinhVe();
        var luoi = Assert.Single(DeckGridBuilder.Build(Golden()), l => l.Cells.Any(o => o.AutoDrawn));

        var html = await VeLuoi(trinh, luoi);

        Assert.Contains("máy bốc thay", html);
        Assert.Contains("người bấm", html);
    }

    [Fact]
    public async Task AC2_OChon_HienChiTietKemMaHoSoGia()
    {
        await using var trinh = new TrinhVe();
        var luoi = Luoi(VongC);
        var o = luoi.Cells.First(x => x.Drawn);

        var html = await VeLuoi(trinh, luoi, viTriChon: o.Position);

        Assert.Contains("chi-tiet-o", html);
        Assert.Contains(o.Draws[0].ApplicantId!, html);
        Assert.Contains(o.Label, html);
    }

    [Fact]
    public async Task AC2_OChuaAiBoc_ChiTietNoiRoKhongCoLuotBocNao_ChuKhongBoTrong()
    {
        await using var trinh = new TrinhVe();
        var luoi = Luoi(VongC);
        var o = luoi.Cells.First(x => !x.Drawn);

        var html = await VeLuoi(trinh, luoi, viTriChon: o.Position);

        Assert.Contains("không có lượt bốc nào", html);
    }

    // ── AC3: bộ lọc hoạt động, thanh tóm tắt khớp số liệu báo cáo ────────────────────────

    [Fact]
    public async Task AC3_KhungLuoi_CoDuBoLocTrungMayBocChuaBoc_VaOTimMaHoSo()
    {
        await using var trinh = new TrinhVe();

        var html = await VeKhung(trinh, Golden());

        Assert.Contains("Chỉ vé trúng", html);
        Assert.Contains("Chỉ vé máy bốc thay", html);
        Assert.Contains("Chỉ ô chưa ai bốc", html);
        Assert.Contains("tim-ho-so", html);
    }

    [Fact]
    public async Task AC3_ThanhTomTat_HienDuNamSoLieuCuaChongPhieu()
    {
        await using var trinh = new TrinhVe();
        var luoi = Luoi(VongC);

        var html = await VeLuoi(trinh, luoi);

        var tomTat = html[html.IndexOf("tom-tat", StringComparison.Ordinal)..];
        foreach (var so in new[]
                 {
                     luoi.Summary.Size, luoi.Summary.WonCount, luoi.Summary.ManualDraws!.Value,
                     luoi.Summary.AutoDraws!.Value, luoi.Summary.UndrawnCells!.Value,
                 })
            Assert.Contains($">{so}<", tomTat);
    }

    [Fact]
    public async Task AC3_KhongCoNhatKyBoc_ThanhTomTat_NoiKhongBiet_ChuKhongHienSoKhong()
    {
        await using var trinh = new TrinhVe();
        var luoi = Assert.Single(DeckGridBuilder.Build(new TransparencyReport
        {
            Decks = [new Deck { Round = "A1", Size = 1, Tickets = ["TRUNG_QUYEN_MUA"] }],
        }));

        var html = await VeLuoi(trinh, luoi);

        Assert.Contains("không công bố", html);
    }

    [Fact]
    public async Task AC3_LocConLaiKhongONao_NoiRo_ChuKhongDeLuoiTrong()
    {
        await using var trinh = new TrinhVe();
        var luoi = Luoi(VongC);

        var html = await VeLuoi(trinh, luoi, hienThi: []);

        Assert.Contains("Không có ô phiếu nào khớp", html);
    }

    // ── AC4: lưới ~900 ô vẫn vẽ được và cuộn mượt trên điện thoại ────────────────────────

    [Fact]
    public async Task AC4_Luoi900O_VeDuOVaChiaKhoiDeTrinhDuyetBoQuaPhanNgoaiManHinh()
    {
        await using var trinh = new TrinhVe();
        var ve = Enumerable.Range(0, 900).Select(i => i % 3 == 0 ? $"TRUNG:2PN-{i:D3}" : "KHONG_TRUNG").ToArray();
        var luoi = Assert.Single(DeckGridBuilder.Build(new TransparencyReport
        {
            Decks = [new Deck { Round = "B:2PN", Size = ve.Length, Tickets = ve }],
        }));

        var dongHo = Stopwatch.StartNew();
        var html = await VeLuoi(trinh, luoi);
        dongHo.Stop();

        Assert.Equal(900, OPhieu.Matches(html).Count);
        Assert.Equal(900 / LuoiOPhieu.SoOMoiKhoi, KhoiO.Matches(html).Count);
        Assert.True(dongHo.ElapsedMilliseconds < 5000, $"Vẽ lưới 900 ô mất {dongHo.ElapsedMilliseconds} ms.");
    }

    // ── AC5: ô chưa có lượt bốc phân biệt rõ với ô đã bốc ────────────────────────────────

    [Fact]
    public async Task AC5_OChuaAiBocVaODaBoc_MangLopKhacNhau()
    {
        await using var trinh = new TrinhVe();
        var luoi = Luoi(VongC);

        var html = await VeLuoi(trinh, luoi);

        Assert.Equal(luoi.Cells.Count(o => o.Drawn), Regex.Matches(html, " da-boc").Count);
        Assert.Equal(luoi.Cells.Count(o => !o.Drawn), Regex.Matches(html, " chua-boc").Count);
    }

    // ── AC6: tìm được ô theo mã định danh hồ sơ ──────────────────────────────────────────

    [Fact]
    public async Task AC6_LuoiVeDungTapOLocDuoc_ChuKhongVeCaChongPhieu()
    {
        await using var trinh = new TrinhVe();
        var luoi = Luoi(VongC);
        var maHoSo = luoi.Cells.First(o => o.Drawn).Draws[0].ApplicantId;

        var html = await VeLuoi(trinh, luoi, hienThi: luoi.Filter(DeckFilter.TatCa, maHoSo));

        Assert.Single(OPhieu.Matches(html));
        Assert.Contains(maHoSo!, html);
    }
}
