using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Kết quả tra cứu block công khai <b>đóng vai</b> thứ mà vỏ UI lấy về từ mạng: fixture chuẩn vàng
/// neo cả ba vòng vào cùng một block, nên chỉ cần một quan sát. Mã băm lấy thẳng từ chính fixture —
/// test nào muốn dựng ca lệch thì tự dựng quan sát khác, chứ bản dùng chung này phải là bản "nguồn
/// công khai đồng ý với báo cáo".
/// </summary>
internal static class QuanSatKhoiGolden
{
    /// <summary>Sau thời điểm chốt cam kết trong fixture (20:26:50.855Z) — đúng chiều lá chắn.</summary>
    public static readonly DateTimeOffset ThoiDiemDao = new(2026, 8, 12, 20, 31, 0, TimeSpan.Zero);

    public const string TenNguon = "nguồn công khai (dựng trong test)";

    public static long DoCao => Bao().AnchorCommitment!.EthTargetHeight!.Value;

    public static string MaBam => Bao().EntropySources![0].BlockHash!;

    public static IReadOnlyList<QuanSatKhoi> DocDuoc() =>
        [new QuanSatKhoi(ChuoiKhoiNeo.Ethereum, DoCao, MaBam, ThoiDiemDao, TenNguon)];

    private static TransparencyReport Bao()
    {
        var ketQua = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(ketQua.Success, ketQua.ErrorMessage);

        return ketQua.Report!;
    }
}
