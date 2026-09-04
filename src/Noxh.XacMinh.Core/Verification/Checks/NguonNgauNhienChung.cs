using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Phần dùng chung của hai hạng mục kiểm nguồn ngẫu nhiên (cam kết máy chủ, hạt giống gốc): cả hai
/// đều chạy cho <b>từng vòng</b> có trong báo cáo và cho kết luận riêng từng vòng.
/// </summary>
internal static class NguonNgauNhienChung
{
    public const string KhongCo = "(không công bố)";

    /// <summary>
    /// Chạy một phép kiểm cho từng vòng. Báo cáo không có khối nguồn ngẫu nhiên thì ra đúng một
    /// kết luận CHƯA ĐỦ DỮ LIỆU — im lặng bỏ qua là để người đọc tưởng hạng mục đã ĐẠT.
    /// </summary>
    public static IEnumerable<CheckResult> TungVong(
        VerificationInput input,
        string id,
        string tenKhiThieuKhoi,
        string giaiThichThieuKhoi,
        Func<EntropySource, string, string, CheckResult> kiem)
    {
        var nguon = input.Report.EntropySources;

        if (nguon is null || nguon.Count == 0)
        {
            yield return new CheckResult(id, tenKhiThieuKhoi, CheckStatus.KhongKiemDuoc, giaiThichThieuKhoi);
            yield break;
        }

        for (var i = 0; i < nguon.Count; i++)
        {
            var ten = Ten(nguon[i], i);
            yield return kiem(nguon[i], $"{id}:{ten}", ten);
        }
    }

    private static string Ten(EntropySource nguon, int index) =>
        string.IsNullOrWhiteSpace(nguon.Round) ? $"#{index + 1}" : nguon.Round!;

    public static string Co(string? giaTri) => string.IsNullOrWhiteSpace(giaTri) ? KhongCo : giaTri.Trim();
}
