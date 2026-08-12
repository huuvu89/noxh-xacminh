using System.Text;
using Noxh.XacMinh.Core.Crypto;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// AC6 — test vector ghim cho phép băm canonical, giá trị lấy từ backend.
/// Vector lấy từ golden test của backend (<c>CryptoFoundationTests.CanonicalDeckSerializer_Golden_ByteIdenticalFormat</c>);
/// mã băm ghim được tính bằng <c>sha256sum</c> trên đúng chuỗi byte đó, không phải bằng chính mã này —
/// tính lại bằng mã đang kiểm thì test chỉ kiểm chính nó.
/// </summary>
public class CanonicalDeckSerializerTests
{
    private static readonly string[] GoldenDeck = { "WIN:2PN-001", "LOSE", "CHO_PHAN_LOAI_DU" };

    private const string GoldenText = "0\tWIN:2PN-001\n1\tLOSE\n2\tCHO_PHAN_LOAI_DU\n";

    private const string GoldenHash = "a2bfab8bce1980b4826554ae5218215d40b6e5ed34d630ec3b28aa39c14d287f";

    [Fact]
    public void AC6_DinhDangCanonical_GhimTheoBackend()
    {
        var bytes = CanonicalDeckSerializer.Serialize(GoldenDeck);

        Assert.Equal(GoldenText, Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void AC6_KhongCoBOM_ODauChuoiByte()
    {
        var bytes = CanonicalDeckSerializer.Serialize(GoldenDeck);

        Assert.Equal((byte)'0', bytes[0]);
    }

    [Fact]
    public void AC6_MaBamGhim_KhopGiaTriTinhDocLap()
    {
        Assert.Equal(GoldenHash, CanonicalDeckSerializer.Hash(GoldenDeck));
    }

    [Fact]
    public void AC6_DoiThuTuVe_DoiMaBam()
    {
        var traoHaiVe = new[] { "WIN:2PN-001", "CHO_PHAN_LOAI_DU", "LOSE" };

        Assert.NotEqual(GoldenHash, CanonicalDeckSerializer.Hash(traoHaiVe));
    }

    [Fact]
    public void AC6_ViTriDanhSoTu0_KhongPhuThuocCulture()
    {
        // Vé rỗng vẫn phải ra dòng "{vị trí}\t\n" — định dạng cố định, không bỏ dòng nào.
        var text = Encoding.UTF8.GetString(CanonicalDeckSerializer.Serialize(new[] { "", "X" }));

        Assert.Equal("0\t\n1\tX\n", text);
    }
}
