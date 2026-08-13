using Noxh.XacMinh.Core.Verification.Checks;

namespace Noxh.XacMinh.Core.Verification;

/// <summary>
/// Seam duy nhất của công cụ: dữ liệu vào — báo cáo kết quả ra, không I/O, không mạng, tất định.
/// Vé sau cắm thêm hạng mục vào <see cref="Checks"/>; vỏ UI không được tự kiểm gì cả.
/// </summary>
public static class Verifier
{
    private static readonly IReadOnlyList<Func<VerificationInput, IEnumerable<CheckResult>>> Checks =
    [
        DeckHashCheck.Run,
        DeckRebuildCheck.Run,
        RServerCommitCheck.Run,
        MasterSeedCheck.Run,
        DrawLogChainCheck.Run,
        DrawTicketMatchCheck.Run,
        ResultsHashCheck.Run,
        FreezeTimestampCheck.Run,
    ];

    public static VerificationReport Verify(VerificationInput input) =>
        new(Checks.SelectMany(check => check(input)).ToList());
}
