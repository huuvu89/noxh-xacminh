using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #3 — chế độ chuyên sâu hiện "giá trị kỳ vọng / tính được / preimage / số liệu thô" cho mọi
/// hạng mục đã có. Phần lõi phải cấp đủ dữ liệu đó; phần vẽ nằm ở test của lớp giao diện.
/// </summary>
public class HangMucSoLieuThoTests
{
    private static VerificationReport ChuanVang()
    {
        var nap = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(nap.Success, nap.ErrorMessage);
        return Verifier.Verify(new VerificationInput(nap.Report!));
    }

    [Fact]
    public void AC2_MoiHangMuc_CapSoLieuThoCoNhanTiengViet()
    {
        Assert.All(ChuanVang().Items, i =>
        {
            Assert.NotEmpty(i.Metrics);
            Assert.All(i.Metrics, m =>
            {
                Assert.False(string.IsNullOrWhiteSpace(m.Label));
                Assert.False(string.IsNullOrWhiteSpace(m.Value));
            });
        });
    }

    [Fact]
    public void AC2_SoLieuTho_CoSoVeCongBo_DeDoiChieuVoiBienBanNiemPhong()
    {
        var item = ChuanVang().Items[0];

        var soVe = Assert.Single(item.Metrics, m => m.Label == "Số vé công bố");
        Assert.Equal(item.Preimage!.TrimEnd('\n').Split('\n').Length.ToString(), soVe.Value);
    }

    [Fact]
    public void AC5_MoiHangMuc_LuonCoCauGiaiThichTiengViet_DuOTrangThaiNao()
    {
        Assert.All(ChuanVang().Items, i => Assert.False(string.IsNullOrWhiteSpace(i.Explanation)));
    }
}
