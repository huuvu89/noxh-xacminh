using Noxh.XacMinh.Core.Crypto;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #5 — AC2: test vector ghim cho lõi chuỗi băm nhật ký bốc.
///
/// Hai vector, hai nguồn độc lập với mã đang kiểm:
/// (a) thứ tự byte của định danh lấy nguyên ví dụ trong <c>backend/docs/domain/lottery-protocol.md</c> §10;
/// (b) mã băm một bước lấy bước đầu tiên của fixture chuẩn vàng — giá trị đó do chính backend tính
///     ra, không phải do lớp này tính. Sinh lại fixture ⇒ cập nhật lại bộ số này (fixtures/README.md).
/// </summary>
public class DrawLogHashChainTests
{
    // Ví dụ ghim trong lottery-protocol.md §10: GUID aabbccdd-eeff-0011-2233-445566778899 nối thành
    // dd cc bb aa  ff ee  11 00  22 33 44 55 66 77 88 99 (mixed-endian .NET, KHÔNG phải RFC 4122).
    private const string GuidMau = "aabbccdd-eeff-0011-2233-445566778899";
    private const string GhimByteDinhDanh = "ddccbbaaffee11002233445566778899";

    // Bước đầu tiên của fixtures/transparency-golden.json (prevHash rỗng).
    private const string ApplicantId = "9fa3bc49-54fa-4a2d-a42f-193b03f957c2";
    private const string DeckId = "019ff7a8-0307-7040-b9a2-09e3819ad2a8";
    private const int Position = 0;
    private const string Payload = "TRUNG_QUYEN_MUA";
    private const string GhimEntryHash = "d78f284d5c3ae30f7075aea13295e607ed54c658e3ecd1783e11205443f3ab51";

    // Chuỗi đem băm của đúng bước đó, ráp tay theo layout đã ghi trong doc: định danh hồ sơ (16B) ‖
    // định danh chồng phiếu (16B) ‖ vị trí int32 big-endian ‖ UTF-8 nội dung vé.
    private const string GhimPreimage =
        "49bca39ffa542d4aa42f193b03f957c2"
        + "a8f79f0107034070b9a209e3819ad2a8"
        + "00000000"
        + "5452554e475f515559454e5f4d5541";

    [Fact]
    public void AC2_ThuTuByteDinhDanh_LaLayoutMixedEndianCuaDotNet_KhongPhaiRfc4122()
    {
        var bytes = DrawLogHashChain.DinhDanhBytes(Guid.Parse(GuidMau));

        Assert.Equal(GhimByteDinhDanh, Convert.ToHexString(bytes).ToLowerInvariant());
    }

    [Fact]
    public void AC2_ChuoiDemBamCuaMotBuoc_GhimTheoLayoutTrongDoc()
    {
        var preimage = DrawLogHashChain.Preimage(
            [], Guid.Parse(ApplicantId), Guid.Parse(DeckId), Position, Payload);

        Assert.Equal(GhimPreimage, Convert.ToHexString(preimage).ToLowerInvariant());
    }

    [Fact]
    public void AC2_MaBamMotBuoc_GhimTheoGiaTriBackendDaSinh()
    {
        var entryHash = DrawLogHashChain.EntryHashHex(
            [], Guid.Parse(ApplicantId), Guid.Parse(DeckId), Position, Payload);

        Assert.Equal(GhimEntryHash, entryHash);
    }

    [Fact]
    public void AC2_DungThuTuByteRfc4122_RaMaBamKhac_DoChinhLaBayCuaVeNay()
    {
        var rfc4122 = Guid.Parse(ApplicantId).ToByteArray();
        Array.Reverse(rfc4122, 0, 4);
        Array.Reverse(rfc4122, 4, 2);
        Array.Reverse(rfc4122, 6, 2);

        Assert.NotEqual(GhimByteDinhDanh[..8], Convert.ToHexString(rfc4122)[..8].ToLowerInvariant());
    }

    [Fact]
    public void AC2_ViTriPhieuVaoPreimageDangInt32BigEndian()
    {
        var preimage = DrawLogHashChain.Preimage([], Guid.Empty, Guid.Empty, 1, string.Empty);

        Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x01 }, preimage[32..36]);
    }

    // ── Thứ tự duyệt tất định: vòng (A1→A2→B→C) → tên vòng → chồng phiếu → vị trí ──────────

    [Theory]
    [InlineData("A1", 0)]
    [InlineData("A2:1PN", 1)]
    [InlineData("B:2PN", 2)]
    [InlineData("C", 3)]
    public void AC2_ThuTuVong_TheoBackend(string round, int thu)
    {
        Assert.Equal(thu, DrawLogHashChain.ThuTuVong(round));
    }

    [Fact]
    public void AC2_VongLa_XepCuoi_ChuKhongNemNgoaiLe()
    {
        Assert.True(DrawLogHashChain.ThuTuVong("vòng lạ") > DrawLogHashChain.ThuTuVong("C"));
    }
}
