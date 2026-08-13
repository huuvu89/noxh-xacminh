using Noxh.XacMinh.Core.Kho;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Noxh.XacMinh.Web.Components;
using Xunit;

namespace Noxh.XacMinh.Web.Tests;

/// <summary>
/// Vé #19 — phần thuộc về màn hình: ba hạng mục đối chiếu trail với báo cáo phải hiện ra, và
/// <b>giới hạn</b> của chúng phải nằm ngay trong câu người kiểm đọc được, không nấp trong tài liệu.
/// Trail chỉ chứng minh được thứ đã lên kho; bản ghi chưa bao giờ được đẩy lên thì nó không thấy —
/// một màn hình toàn màu xanh mà im chuyện đó là kiểu nói dối tệ nhất của công cụ này.
/// </summary>
public class TrailDoiChieuHienThiTests
{
    private const string GioiHan = "chưa bao giờ được đẩy lên";

    private static readonly string[] TenHangMuc =
    [
        "lượt bốc đối chiếu với nhật ký công bố",
        "cam kết ngẫu nhiên máy chủ",
        "đầu chuỗi băm từng vòng",
    ];

    private static TransparencyReport BaoCao()
    {
        var nap = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(nap.Success, nap.ErrorMessage);

        return nap.Report!;
    }

    private static Task<string> Ve(TrinhVe trinh, KhoBangChung? kho) =>
        trinh.Ve<KetQuaKiem>(new Dictionary<string, object?>
        {
            ["BaoCao"] = Verifier.Verify(new VerificationInput(BaoCao(), Kho: kho)),
            ["Nguon"] = "báo cáo.json",
        });

    [Fact]
    public async Task AC5_ChuaDocKho_BaHangMucVanHienRa_KemGioiHanVaKetLuanKhongKiemDuoc()
    {
        await using var trinh = new TrinhVe();

        var html = await Ve(trinh, null);

        Assert.All(TenHangMuc, ten => Assert.Contains(ten, html, StringComparison.Ordinal));
        Assert.Contains(GioiHan, html, StringComparison.Ordinal);
        Assert.Contains("KHÔNG KIỂM ĐƯỢC", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Lượt bốc có trên trail mà thiếu trong báo cáo là phát hiện nặng nhất của vé này — nó phải hiện
    /// nguyên văn trên màn hình, kèm đích danh ô phiếu, chứ không phải một dòng đỏ chung chung.
    /// </summary>
    [Fact]
    public async Task AC5_LuotBocCoTrenTrailMaBaoCaoKhongCo_ManHinhNoiThangKhongDat_VaVanNeuGioiHan()
    {
        var kho = KhoBangChung.Doc(CheDoDocKho.AnDanh, DungLoTrailWeb.LoLuotBocLa(BaoCao().ProjectId!), "kho thử");

        await using var trinh = new TrinhVe();

        var html = await Ve(trinh, kho);

        Assert.Contains("KHÔNG ĐẠT", html, StringComparison.Ordinal);
        Assert.Contains("vị trí 9001", html, StringComparison.Ordinal);
        Assert.Contains(GioiHan, html, StringComparison.Ordinal);
    }
}
