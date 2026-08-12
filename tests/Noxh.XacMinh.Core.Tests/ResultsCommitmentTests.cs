using Noxh.XacMinh.Core.Crypto;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// AC5 — test vector ghim cho phép băm bảng kết quả, bộ dòng lấy từ golden test của backend
/// (<c>ResultsCommitmentTests.HashHex_CanonicalDungDinhDang_NullThanhGachNgang</c>). Mã băm ghim
/// tính bằng <c>sha256sum</c> trên đúng chuỗi byte canonical, không phải bằng chính mã này — tính
/// lại bằng mã đang kiểm thì test chỉ kiểm chính nó.
/// </summary>
public class ResultsCommitmentTests
{
    private static readonly Guid A = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static readonly Guid B = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static readonly ResultsCommitment.Row[] GoldenRows =
    [
        new(A, true, "Regular", "2PN", "A-12-03", null),
        new(B, false, "LeftoverWaitlist", null, null, 5)
    ];

    private const string GoldenText =
        "aaaaaaaa-0000-0000-0000-000000000001\t1\tRegular\t2PN\tA-12-03\t-\n"
        + "bbbbbbbb-0000-0000-0000-000000000002\t0\tLeftoverWaitlist\t-\t-\t5\n";

    private const string GoldenHash = "851b5109fc87ee6ab15890c4b69c2f887af73271a93847e261350a514fe3aab9";

    [Fact]
    public void AC5_DinhDangCanonical_GhimTheoBackend_NullThanhGachNgang()
    {
        Assert.Equal(GoldenText, ResultsCommitment.CanonicalText(GoldenRows));
    }

    [Fact]
    public void AC5_MaBamGhim_KhopGiaTriTinhDocLap()
    {
        Assert.Equal(GoldenHash, ResultsCommitment.HashHex(GoldenRows));
    }

    [Fact]
    public void AC5_KhongPhuThuocThuTuDongDauVao()
    {
        Assert.Equal(GoldenHash, ResultsCommitment.HashHex(GoldenRows.Reverse()));
    }

    [Fact]
    public void AC5_SuaMaCan_DoiMaBam()
    {
        var sua = GoldenRows.Select(r => r.UnitCode is null ? r : r with { UnitCode = "A-12-04" });

        Assert.NotEqual(GoldenHash, ResultsCommitment.HashHex(sua));
    }

    [Fact]
    public void AC5_SuaHangDuKhuyet_DoiMaBam()
    {
        var sua = GoldenRows.Select(r => r.WaitlistRank is null ? r : r with { WaitlistRank = 4 });

        Assert.NotEqual(GoldenHash, ResultsCommitment.HashHex(sua));
    }

    [Fact]
    public void AC5_HangDuKhuyet_KhongPhuThuocCulture()
    {
        // Số phải ra "5" ở mọi culture — locale dùng chữ số khác sẽ đổi cả mã băm.
        var truoc = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
        try
        {
            Assert.Equal(GoldenHash, ResultsCommitment.HashHex(GoldenRows));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = truoc;
        }
    }
}
