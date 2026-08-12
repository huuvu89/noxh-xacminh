namespace Noxh.XacMinh.Core.Verification;

/// <summary>
/// Kết luận của một hạng mục kiểm. <see cref="Explanation"/> nói "cái này chứng minh điều gì" bằng
/// tiếng Việt cho người thường; <see cref="Expected"/>/<see cref="Actual"/>/<see cref="Preimage"/>
/// dành cho chế độ chuyên sâu, để người kiểm toán tự tính lại bằng công cụ khác.
/// </summary>
public sealed record CheckResult(
    string Id,
    string Title,
    CheckStatus Status,
    string Explanation,
    string? Expected = null,
    string? Actual = null,
    string? Preimage = null);

/// <summary>Mã hạng mục — UI và bản xuất kết quả bám vào đây, không bám vào tiêu đề hiển thị.</summary>
public static class CheckIds
{
    public const string DeckHash = "deck-hash";
}
