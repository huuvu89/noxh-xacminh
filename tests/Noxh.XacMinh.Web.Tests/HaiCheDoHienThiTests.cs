using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Noxh.XacMinh.Web.Components;
using Noxh.XacMinh.Web.HienThi;
using Xunit;

namespace Noxh.XacMinh.Web.Tests;

/// <summary>
/// Vé #3 — khuôn hiển thị hai chế độ. Tám vé hạng mục kiểm sau cắm vào đúng khuôn này, nên test đi
/// qua component thật (<see cref="KetQuaKiem"/>) chứ không qua một model trung gian tự bịa.
/// </summary>
public class HaiCheDoHienThiTests
{
    private const string HexDai = "3f2a91c4b7e8d0562a1b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8f";

    /// <summary>Chuỗi hex dài — thứ làm người dân ngợp; chế độ người dân không được để lọt ra.</summary>
    private static readonly Regex HexLoangNgoang = new("[0-9a-fA-F]{16,}", RegexOptions.Compiled);

    private static VerificationReport BaoCaoBaTrangThai() => new(
    [
        new CheckResult(
            "hang-muc-dat", "Mã băm chồng phiếu A1", CheckStatus.Dat,
            "Nội dung vé công bố khớp mã băm đã niêm phong.",
            Expected: HexDai, Actual: HexDai, Preimage: "0\tVE-0001\n1\tVE-0002\n")
        {
            Metrics = [new CheckMetric("Số vé công bố", "892")],
        },
        new CheckResult(
            "hang-muc-khong-dat", "Mã băm chồng phiếu B", CheckStatus.KhongDat,
            "Mã băm tính lại KHÁC mã băm đã niêm phong.",
            Expected: HexDai, Actual: HexDai.Replace('3', '4'), Preimage: "0\tVE-0009\n")
        {
            Metrics = [new CheckMetric("Số vé công bố", "77")],
        },
        new CheckResult(
            "hang-muc-khong-kiem-duoc", "Mã băm chồng phiếu C", CheckStatus.KhongKiemDuoc,
            "Chồng phiếu này chưa công bố nội dung vé, nên chưa kiểm được."),
    ]);

    private static VerificationReport BaoCaoChuanVang()
    {
        var nap = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(nap.Success, nap.ErrorMessage);
        return Verifier.Verify(new VerificationInput(nap.Report!));
    }

    private static Dictionary<string, object?> ThamSo(VerificationReport baoCao, string nguon = "báo cáo.json") =>
        new() { ["BaoCao"] = baoCao, ["Nguon"] = nguon };

    private static Task<string> Ve(TrinhVe trinh, VerificationReport baoCao) =>
        trinh.Ve<KetQuaKiem>(ThamSo(baoCao));

    // ── AC1: mặc định là chế độ người dân, không hiện hex ────────────────────────────────

    [Fact]
    public async Task AC1_MacDinh_LaCheDoNguoiDan_KhongHienChuoiHex()
    {
        await using var trinh = new TrinhVe();

        var html = await Ve(trinh, BaoCaoBaTrangThai());

        Assert.Equal(CheDoHienThi.NguoiDan, trinh.TrangThai.CheDo);
        Assert.DoesNotMatch(HexLoangNgoang, html);
    }

    [Fact]
    public async Task AC1_CheDoNguoiDan_KhongHienPreimageVaSoLieuTho()
    {
        await using var trinh = new TrinhVe();

        var html = await Ve(trinh, BaoCaoBaTrangThai());

        Assert.DoesNotContain("VE-0001", html);
        Assert.DoesNotContain("Số vé công bố", html);
    }

    [Fact]
    public async Task AC1_MacDinh_BaoCaoChuanVangThat_KhongHienChuoiHex()
    {
        await using var trinh = new TrinhVe();

        var html = await Ve(trinh, BaoCaoChuanVang());

        Assert.DoesNotMatch(HexLoangNgoang, html);
    }

    // ── AC2: công tắc chuyên sâu hiện kỳ vọng / tính được / preimage cho mọi hạng mục ────

    [Fact]
    public async Task AC2_CheDoChuyenSau_HienKyVongTinhDuocPreimage_ChoMoiHangMucDaCo()
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(CheDoHienThi.ChuyenSau);

        var baoCao = BaoCaoBaTrangThai();
        var html = await Ve(trinh, baoCao);

        foreach (var item in baoCao.Items)
        {
            if (item.Expected is not null) Assert.Contains(item.Expected, html);
            if (item.Actual is not null) Assert.Contains(item.Actual, html);
            if (item.Preimage is not null) Assert.Contains("VE-", html);
        }
    }

    [Fact]
    public async Task AC2_CheDoChuyenSau_HienSoLieuTho()
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(CheDoHienThi.ChuyenSau);

        var html = await Ve(trinh, BaoCaoBaTrangThai());

        Assert.Contains("Số vé công bố", html);
        Assert.Contains("892", html);
    }

    [Fact]
    public async Task AC2_CheDoChuyenSau_HangMucThieuDuLieu_NoiRoLaChuaTinhDuoc_KhongBoTrong()
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(CheDoHienThi.ChuyenSau);

        var thieu = new VerificationReport([BaoCaoBaTrangThai().Items[2]]);
        var html = await Ve(trinh, thieu);

        Assert.Contains("chưa tính được", html);
    }

    [Fact]
    public async Task AC2_CheDoChuyenSau_BaoCaoChuanVangThat_HienMaBamCuaMoiChongPhieu()
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(CheDoHienThi.ChuyenSau);

        var baoCao = BaoCaoChuanVang();
        var html = await Ve(trinh, baoCao);

        Assert.All(baoCao.Items, i => Assert.Contains(i.Expected!, html));
    }

    // ── AC3: lựa chọn chế độ giữ nguyên khi nạp file mới trong cùng phiên ─────────────────

    [Fact]
    public async Task AC3_ChonChuyenSau_RoiNapBaoCaoMoi_VanConChuyenSau()
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(CheDoHienThi.ChuyenSau);

        await Ve(trinh, BaoCaoBaTrangThai());
        var htmlFileMoi = await Ve(trinh, BaoCaoChuanVang());

        Assert.Equal(CheDoHienThi.ChuyenSau, trinh.TrangThai.CheDo);
        Assert.Contains("Giá trị kỳ vọng", htmlFileMoi);
    }

    [Fact]
    public async Task AC3_CongTac_VeTheoCheDoDangChon_ChuKhongVeLaiTuDau()
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(CheDoHienThi.ChuyenSau);

        await Ve(trinh, BaoCaoBaTrangThai());
        var htmlFileMoi = await trinh.Ve<CongTacCheDo>([]);

        Assert.Contains("checked", htmlFileMoi);
    }

    [Fact]
    public void AC3_TrangThaiHienThi_SongTheoPhien_ChuKhongTheoLanNapFile()
    {
        var services = new ServiceCollection();
        services.ThemHienThi();

        var mota = Assert.Single(services, d => d.ServiceType == typeof(TrangThaiHienThi));

        // Scoped trong Blazor WebAssembly = một bản duy nhất cho cả phiên tab.
        Assert.Equal(ServiceLifetime.Scoped, mota.Lifetime);
    }

    [Fact]
    public void AC3_Program_DangKyTrangThaiHienThi_KeoKhongCoDichVuLucChay()
    {
        var program = File.ReadAllText(RepoPaths.Src("Noxh.XacMinh.Web", "Program.cs"));

        Assert.Contains("ThemHienThi()", program);
    }

    // ── AC4: kết luận tổng hiện rõ ở đầu trang, ở cả hai chế độ ───────────────────────────

    [Theory]
    [InlineData(CheDoHienThi.NguoiDan)]
    [InlineData(CheDoHienThi.ChuyenSau)]
    public async Task AC4_KetLuanTong_HienNgayDauTrang(CheDoHienThi cheDo)
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(cheDo);

        var html = await Ve(trinh, BaoCaoBaTrangThai());

        var ketLuan = html.IndexOf("ket-luan", StringComparison.Ordinal);
        var hangMucDau = html.IndexOf("hang-muc", StringComparison.Ordinal);

        Assert.InRange(ketLuan, 0, int.MaxValue);
        Assert.True(ketLuan < hangMucDau, "Kết luận tổng phải nằm trên danh sách hạng mục.");
        Assert.Contains(CheckStatus.KhongDat.Nhan(), html);
    }

    [Theory]
    [InlineData(CheDoHienThi.NguoiDan)]
    [InlineData(CheDoHienThi.ChuyenSau)]
    public async Task AC4_KetLuanTong_ThieuDuLieu_KhongDeMauXanh(CheDoHienThi cheDo)
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(cheDo);

        var thieu = new VerificationReport([BaoCaoBaTrangThai().Items[2]]);
        var html = await Ve(trinh, thieu);

        Assert.Contains("khong-kiem-duoc", html);
        Assert.Contains(CheckStatus.KhongKiemDuoc.Nhan(), html);
    }

    // ── AC5: mỗi hạng mục có câu giải thích tiếng Việt cho người không rành kỹ thuật ──────

    [Theory]
    [InlineData(CheDoHienThi.NguoiDan)]
    [InlineData(CheDoHienThi.ChuyenSau)]
    public async Task AC5_MoiHangMuc_CoCauGiaiThichTiengViet(CheDoHienThi cheDo)
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(cheDo);

        var baoCao = BaoCaoBaTrangThai();
        var html = await Ve(trinh, baoCao);

        Assert.All(baoCao.Items, i => Assert.Contains(i.Explanation, html));
    }

    [Fact]
    public async Task AC5_BaoCaoChuanVangThat_MoiHangMucDeuCoGiaiThich()
    {
        await using var trinh = new TrinhVe();

        var baoCao = BaoCaoChuanVang();
        var html = await Ve(trinh, baoCao);

        Assert.All(baoCao.Items, i =>
        {
            Assert.False(string.IsNullOrWhiteSpace(i.Explanation));
            Assert.Contains(i.Explanation, html);
        });
    }
}
