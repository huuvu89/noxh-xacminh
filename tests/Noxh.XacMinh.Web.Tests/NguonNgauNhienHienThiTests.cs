using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Noxh.XacMinh.Web.Components;
using Noxh.XacMinh.Web.HienThi;
using Xunit;

namespace Noxh.XacMinh.Web.Tests;

/// <summary>
/// Vé #4 AC5 — chế độ chuyên sâu hiện đủ ba nguồn đầu vào của hạt giống. Đi qua component thật với
/// fixture chuẩn vàng: lõi cấp đủ số liệu mà khuôn hiển thị nuốt mất thì người kiểm vẫn không thấy.
/// </summary>
public class NguonNgauNhienHienThiTests
{
    private static TransparencyReport ChuanVang()
    {
        var nap = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(nap.Success, nap.ErrorMessage);
        return nap.Report!;
    }

    private static Task<string> Ve(TrinhVe trinh, TransparencyReport baoCao) =>
        trinh.Ve<KetQuaKiem>(new Dictionary<string, object?>
        {
            ["BaoCao"] = Verifier.Verify(new VerificationInput(baoCao)),
            ["Nguon"] = "báo cáo.json",
        });

    [Fact]
    public async Task AC5_CheDoChuyenSau_HienDuBaNguonDauVaoCuaHatGiong_ChoTungVong()
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.Dat(CheDoHienThi.ChuyenSau);

        var baoCao = ChuanVang();
        var html = await Ve(trinh, baoCao);

        Assert.Contains("Ngẫu nhiên máy chủ (R_server)", html);
        Assert.Contains("Ngẫu nhiên tổ giám sát (R_supervisor)", html);
        Assert.Contains("Mã băm khối neo (H_blockchain)", html);

        Assert.All(baoCao.EntropySources!, nguon =>
        {
            Assert.Contains(nguon.RServer!, html);
            Assert.Contains(nguon.RSupervisor!, html);
            Assert.Contains(nguon.BlockHash!, html);
            Assert.Contains(nguon.MasterSeed!, html);
        });
    }

    [Fact]
    public async Task AC1_CheDoNguoiDan_KetLuanTungVong_KhongLoNguonNgauNhien()
    {
        await using var trinh = new TrinhVe();

        var baoCao = ChuanVang();
        var html = await Ve(trinh, baoCao);

        Assert.All(baoCao.EntropySources!, nguon =>
        {
            // Từng vòng một dòng kết luận riêng cho cả hai hạng mục…
            Assert.Contains($"Cam kết ngẫu nhiên máy chủ vòng {nguon.Round}", html);
            Assert.Contains($"Hạt giống gốc vòng {nguon.Round}", html);
            // …nhưng người dân không phải nhìn chuỗi hex nào.
            Assert.DoesNotContain(nguon.RServer!, html);
            Assert.DoesNotContain(nguon.MasterSeed!, html);
        });
    }
}
