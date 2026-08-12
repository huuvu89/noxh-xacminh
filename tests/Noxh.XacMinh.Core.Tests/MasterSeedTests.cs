using System.Text;
using Noxh.XacMinh.Core.Crypto;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #4 — test vector ghim cho lõi hạt giống. Ba nguồn lấy đúng bộ đầu vào của golden test backend
/// (<c>CryptoFoundationTests.MasterSeed_And_RoundSeed_AreDeterministic</c>); mã băm ghim tính bằng
/// <c>sha256sum</c> trên đúng chuỗi byte đó, không phải bằng chính mã đang kiểm.
/// </summary>
public class MasterSeedTests
{
    private static readonly byte[] RServer = Encoding.UTF8.GetBytes("server");
    private static readonly byte[] RSupervisor = Encoding.UTF8.GetBytes("supervisor");
    private static readonly byte[] HBlockchain = Encoding.UTF8.GetBytes("blockhash");

    private const string GhimMasterSeed = "0b2978c903fb171080b40fbd4c008e7fcf78887415eb20936f5f908f79155b60";

    [Fact]
    public void AC1_HatGiong_GhimTheoBackend_NoiByteThoKhongDauPhanCach()
    {
        var seed = MasterSeed.Build(RServer, RSupervisor, HBlockchain);

        Assert.Equal(GhimMasterSeed, Convert.ToHexString(seed).ToLowerInvariant());
    }

    [Fact]
    public void AC1_DoiThuTuBaNguon_DoiHatGiong()
    {
        var daoThuTu = MasterSeed.Build(RSupervisor, RServer, HBlockchain);

        Assert.NotEqual(GhimMasterSeed, Convert.ToHexString(daoThuTu).ToLowerInvariant());
    }

    // ── Đọc hex khoan dung: file người dùng thả vào không ra ngoại lệ ────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("khong-phai-hex")]
    [InlineData("abc")] // lẻ nửa byte
    public void AC2_HexHong_TraVeNull_ChuKhongNemNgoaiLe(string hong)
    {
        Assert.Null(Hex.Doc(hong));
    }

    [Fact]
    public void AC2_HexVietHoa_VaCoKhoangTrang_VanDocDuoc_VaChuanHoaVeChuThuong()
    {
        Assert.Equal([0xAB, 0xCD], Hex.Doc("  ABCD  "));
        Assert.Equal("abcd", Hex.ChuanHoa("  ABCD  "));
    }
}
