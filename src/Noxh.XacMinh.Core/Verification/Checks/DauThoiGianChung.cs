using System.Globalization;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Phần dùng chung của hai hạng mục đụng tới dấu thời gian mốc cam kết (nội dung được đóng dấu, và
/// mốc neo chuỗi khối). Hai hạng mục phải lọc ra <b>cùng một tập dấu</b> và đọc mốc thời gian theo
/// <b>cùng một quy tắc</b>, kẻo màn hình nói hai chuyện khác nhau về cùng một token.
/// </summary>
internal static class DauThoiGianChung
{
    public const string ScopeMocCamKet = "FREEZE";

    /// <summary>
    /// Dấu của mốc cam kết, thứ tự cố định để cùng một file luôn ra cùng một báo cáo bất kể thứ tự
    /// dòng trong file.
    /// </summary>
    public static List<TimestampToken> MocCamKet(IEnumerable<TimestampToken> dau) =>
        dau.Where(t => string.Equals(t.Scope?.Trim(), ScopeMocCamKet, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.GenTime ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(t => t.Authority ?? string.Empty, StringComparer.Ordinal)
            .ToList();

    public static List<TimestampToken> TatCa(TransparencyReport bc) =>
        (bc.Timestamps ?? []).Where(t => t is not null).Select(t => t!).ToList();

    /// <summary>Mốc thời gian cắt về millisecond — đúng độ chính xác mà chuỗi đóng dấu ghim.</summary>
    public static DateTimeOffset? DocMoc(string? giaTri) =>
        DateTimeOffset.TryParse(
            giaTri,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var moc)
            ? new DateTimeOffset(moc.Ticks - moc.Ticks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero)
            : null;

    /// <summary>Mốc thời gian hiện cho người đọc: luôn quy về UTC, không phụ thuộc máy đang xem.</summary>
    public static string HienThi(DateTimeOffset moc) =>
        moc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
}
