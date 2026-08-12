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
    string? Preimage = null)
{
    /// <summary>Số liệu thô của hạng mục (chỉ hiện ở chế độ chuyên sâu), ví dụ số vé, quy mô deck.</summary>
    public IReadOnlyList<CheckMetric> Metrics { get; init; } = [];

    // So sánh theo giá trị, kể cả Metrics: hai lần kiểm trên cùng dữ liệu phải ra kết quả bằng nhau
    // (dán nội dung == thả file). Danh sách so sánh theo tham chiếu sẽ phá điều đó.
    // THÊM TRƯỜNG MỚI THÌ THÊM VÀO ĐÂY — CheckResultEqualityTests là hàng rào.
    public bool Equals(CheckResult? other) =>
        other is not null
        && Id == other.Id
        && Title == other.Title
        && Status == other.Status
        && Explanation == other.Explanation
        && Expected == other.Expected
        && Actual == other.Actual
        && Preimage == other.Preimage
        && Metrics.SequenceEqual(other.Metrics);

    public override int GetHashCode() =>
        HashCode.Combine(Id, Title, Status, Explanation, Expected, Actual, Preimage, Metrics.Count);
}

/// <summary>Một số liệu thô có nhãn tiếng Việt — khuôn hiển thị vẽ thẳng, không diễn giải gì thêm.</summary>
public sealed record CheckMetric(string Label, string Value);

/// <summary>Mã hạng mục — UI và bản xuất kết quả bám vào đây, không bám vào tiêu đề hiển thị.</summary>
public static class CheckIds
{
    public const string DeckHash = "deck-hash";

    public const string RServerCommit = "r-server-commit";

    public const string MasterSeed = "master-seed";

    public const string DrawLogChain = "draw-log-chain";

    public const string DrawTicketMatch = "draw-ticket-match";
}
