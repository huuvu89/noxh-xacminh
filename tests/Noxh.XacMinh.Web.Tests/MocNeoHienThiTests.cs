using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Noxh.XacMinh.Web.Components;
using Noxh.XacMinh.Web.HienThi;
using Noxh.XacMinh.Web.MocNeo;
using Xunit;

namespace Noxh.XacMinh.Web.Tests;

/// <summary>
/// Vé #15 — phần thuộc về vỏ giao diện: việc gọi mạng nằm ở đây, và khi nó hỏng thì màn hình phải
/// đưa được người kiểm tới link tra cứu thủ công. Lõi cấp đủ dữ liệu mà khuôn hiển thị nuốt mất thì
/// người kiểm vẫn không thấy gì.
/// </summary>
public class MocNeoHienThiTests
{
    private static TransparencyReport ChuanVang()
    {
        var nap = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(nap.Success, nap.ErrorMessage);

        return nap.Report!;
    }

    private static string LinkChuanVang(TransparencyReport bc) =>
        $"https://etherscan.io/block/{bc.AnchorCommitment!.EthTargetHeight!.Value}";

    private static Task<string> VeKetQua(TrinhVe trinh, VerificationInput input) =>
        trinh.Ve<KetQuaKiem>(new Dictionary<string, object?>
        {
            ["BaoCao"] = Verifier.Verify(input),
            ["Nguon"] = "báo cáo.json",
        });

    private static Task<string> VeMocNeo(TrinhVe trinh) =>
        trinh.Ve<MocNeoChuoiKhoi>(new Dictionary<string, object?>());

    // ── AC4: chưa tra cứu / hỏi không được ⇒ link tra cứu thủ công hiện ngay ở kết luận ──

    [Fact]
    public async Task AC4_ChuaTraCuuDuocBlock_CheDoNguoiDan_VanThayLinkTraCuuThuCong()
    {
        await using var trinh = new TrinhVe();
        var baoCao = ChuanVang();

        var html = await VeKetQua(trinh, new VerificationInput(baoCao));

        Assert.Contains("CHƯA ĐỦ DỮ LIỆU", html, StringComparison.Ordinal);
        Assert.Contains(LinkChuanVang(baoCao), html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC4_NguonNgoaiLoi_ManHinhNoiRoLoi_VaVanCoLinkTraCuuThuCong()
    {
        var baoCao = ChuanVang();
        var mocNeo = new TrangThaiMocNeo((yeu, _) =>
            Task.FromResult(new QuanSatKhoi(yeu.ChuoiKhoi, yeu.DoCao, Loi: "trình duyệt chặn (CORS)")));
        mocNeo.DatBaoCao(baoCao);
        await mocNeo.TraCuu();

        await using var trinh = new TrinhVe(mocNeo: mocNeo);

        var ketQua = await VeKetQua(trinh, new VerificationInput(baoCao, null, mocNeo.DaDoc));
        var khungTraCuu = await VeMocNeo(trinh);

        Assert.Contains("trình duyệt chặn (CORS)", ketQua, StringComparison.Ordinal);
        Assert.Contains(LinkChuanVang(baoCao), ketQua, StringComparison.Ordinal);
        Assert.Contains("hỏi không được", khungTraCuu, StringComparison.Ordinal);
        Assert.Contains(LinkChuanVang(baoCao), khungTraCuu, StringComparison.Ordinal);
    }

    // ── AC5: gọi mạng là việc của vỏ, và chỉ chạy khi người dùng bấm ─────────────────────

    [Fact]
    public async Task AC5_ChuaBamTraCuu_KhungTraCuuKhongTuGoiMang_ChiMoiNut()
    {
        // TrinhVe cấp một hàm đọc block ném ngoại lệ: chỉ cần vẽ mà lỡ gọi mạng là test đỏ.
        await using var trinh = new TrinhVe();
        trinh.MocNeo.DatBaoCao(ChuanVang());

        var html = await VeMocNeo(trinh);

        Assert.Contains("Tra cứu block trên nguồn công khai", html, StringComparison.Ordinal);
        Assert.Contains("chưa tra cứu", html, StringComparison.Ordinal);
        Assert.Contains("không có dữ liệu nào của bạn rời khỏi máy này", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC5_ChuaNapBaoCao_KhungTraCuuNoiRoChuaCoMocNeoNaoDeTraCuu()
    {
        await using var trinh = new TrinhVe();

        var html = await VeMocNeo(trinh);

        Assert.Contains("Chưa có mốc neo nào để tra cứu", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Tra cứu block trên nguồn công khai", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC1_TraCuuXong_KetLuanDoiSangDat_VaKhungTraCuuNoiRoNguonDaHoi()
    {
        var baoCao = ChuanVang();
        var mocNeo = new TrangThaiMocNeo((yeu, _) => Task.FromResult(new QuanSatKhoi(
            yeu.ChuoiKhoi,
            yeu.DoCao,
            baoCao.EntropySources![0].BlockHash,
            new DateTimeOffset(2026, 8, 12, 20, 31, 0, TimeSpan.Zero),
            "nguồn công khai (dựng trong test)")));
        mocNeo.DatBaoCao(baoCao);
        await mocNeo.TraCuu();

        await using var trinh = new TrinhVe(mocNeo: mocNeo);

        var ketQua = await VeKetQua(trinh, new VerificationInput(baoCao, null, mocNeo.DaDoc));
        var khungTraCuu = await VeMocNeo(trinh);

        Assert.Contains("Mốc neo chuỗi khối vòng A", ketQua, StringComparison.Ordinal);
        Assert.Contains("thử đi thử lại", ketQua, StringComparison.Ordinal);
        Assert.Contains("đã đọc từ nguồn công khai (dựng trong test)", khungTraCuu, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC5_NapBaoCaoKhac_QuenBlockDaDocCuaBaoCaoTruoc()
    {
        var mocNeo = new TrangThaiMocNeo((yeu, _) =>
            Task.FromResult(new QuanSatKhoi(yeu.ChuoiKhoi, yeu.DoCao, new string('b', 64))));
        mocNeo.DatBaoCao(ChuanVang());
        await mocNeo.TraCuu();
        Assert.NotEmpty(mocNeo.DaDoc);

        mocNeo.DatBaoCao(ChuanVang());

        Assert.Empty(mocNeo.DaDoc);
        Assert.Equal(BuocTraCuuNeo.ChuaTraCuu, mocNeo.Buoc);
    }
}
