namespace Noxh.XacMinh.Core.Verification;

public sealed record VerificationReport(IReadOnlyList<CheckResult> Items)
{
    /// <summary>
    /// Kết luận chung. Một hạng mục KHÔNG KIỂM ĐƯỢC cũng kéo kết luận chung xuống — màn hình toàn
    /// màu xanh trong khi có hạng mục chưa kiểm được là kiểu nói dối nguy hiểm nhất của công cụ này.
    /// </summary>
    public CheckStatus Overall =>
        Items.Count == 0 ? CheckStatus.KhongKiemDuoc
        : Items.Any(i => i.Status == CheckStatus.KhongDat) ? CheckStatus.KhongDat
        : Items.Any(i => i.Status == CheckStatus.KhongKiemDuoc) ? CheckStatus.KhongKiemDuoc
        : CheckStatus.Dat;
}
