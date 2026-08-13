using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Noxh.XacMinh.Core.Kho;

/// <summary>
/// Một object đọc nguyên byte từ kho — chưa bóc gì, để mã băm tính trên đúng byte đã đọc.
/// <see cref="Loi"/> khác <c>null</c> nghĩa là object <b>có</b> trong danh sách kho nhưng tải nội
/// dung không được: khác hẳn với object biến mất khỏi danh sách, và không được vu cho ai cả.
/// </summary>
public sealed record DoiTuongKho(string Key, byte[] NoiDung, string? Loi = null);

/// <summary>
/// Một bản ghi bằng chứng trong lô. <see cref="PayloadJson"/> giữ nguyên chuỗi JSON gốc: vé sau đối
/// chiếu trail với báo cáo minh bạch cần đúng nội dung đã được đẩy lên, không phải bản đã diễn giải
/// lại theo model của công cụ.
/// </summary>
public sealed record BanGhiTrail(string? Loai, string? MaDuAn, DateTimeOffset? XayRaLuc, string PayloadJson);

/// <summary>
/// Một lô bằng chứng đã bóc: dòng đầu là <c>BATCH_HEADER</c> mang số thứ tự lô, mã tiến trình đẩy
/// lô, và <c>(prevKey, prevSha256)</c> của lô liền trước — chính hai trường đó móc các object trên
/// kho thành một chuỗi, nên giấu bớt một object ở giữa là lô sau tố cáo ngay.
///
/// <see cref="Loi"/> khác <c>null</c> nghĩa là lô không bóc được (không phải JSONL, thiếu dòng đầu):
/// chưa kiểm được đoạn chuỗi đó, chưa phải bằng chứng có người sửa.
/// </summary>
public sealed record LoBangChung(
    string Key,
    string Sha256,
    long? SoLo,
    string? MaTienTrinh,
    string? PhienBan,
    DateTimeOffset? TaoLuc,
    int? SoBanGhiKhai,
    string? KeyLoTruoc,
    string? Sha256LoTruoc,
    IReadOnlyList<BanGhiTrail> BanGhi,
    string? Loi)
{
    public const string PhienBanChuan = "NOXH-TRAIL-v1";
}

/// <summary>Bóc một object trên kho thành lô bằng chứng — hàm thuần, không mạng, không I/O.</summary>
public static class DocLoBangChung
{
    private const string LoaiDongDau = "BATCH_HEADER";

    public static LoBangChung Doc(DoiTuongKho doiTuong)
    {
        var sha = Convert.ToHexString(SHA256.HashData(doiTuong.NoiDung)).ToLowerInvariant();

        LoBangChung Hong(string loi) =>
            new(doiTuong.Key, sha, null, null, null, null, null, null, null, [], loi);

        if (doiTuong.Loi is not null) return Hong(doiTuong.Loi);

        var dong = Encoding.UTF8.GetString(doiTuong.NoiDung)
            .Split('\n')
            .Where(d => d.Trim().Length > 0)
            .ToList();

        if (dong.Count == 0) return Hong("lô rỗng");

        JsonElement dau;

        try
        {
            dau = JsonDocument.Parse(dong[0]).RootElement.Clone();
        }
        catch (JsonException ex)
        {
            return Hong($"dòng đầu không phải JSON đọc được: {ex.Message}");
        }

        if (dau.ValueKind != JsonValueKind.Object || Chuoi(dau, "kind") != LoaiDongDau)
            return Hong($"dòng đầu không phải {LoaiDongDau} — lô này không theo khuôn kho bằng chứng");

        var banGhi = new List<BanGhiTrail>();

        for (var i = 1; i < dong.Count; i++)
        {
            JsonElement ban;

            try
            {
                ban = JsonDocument.Parse(dong[i]).RootElement.Clone();
            }
            catch (JsonException ex)
            {
                return Hong($"bản ghi thứ {i} không phải JSON đọc được: {ex.Message}");
            }

            banGhi.Add(new BanGhiTrail(
                Chuoi(ban, "kind"),
                Chuoi(ban, "projectId"),
                ThoiDiem(ban, "occurredAt"),
                ban.TryGetProperty("payload", out var payload) ? payload.GetRawText() : string.Empty));
        }

        return new LoBangChung(
            doiTuong.Key,
            sha,
            So(dau, "batchNo"),
            Chuoi(dau, "instanceId"),
            Chuoi(dau, "version"),
            ThoiDiem(dau, "createdAt"),
            (int?)So(dau, "recordCount"),
            Chuoi(dau, "prevKey"),
            Chuoi(dau, "prevSha256"),
            banGhi,
            null);
    }

    private static string? Chuoi(JsonElement doi, string ten) =>
        doi.TryGetProperty(ten, out var giaTri) && giaTri.ValueKind == JsonValueKind.String
            ? giaTri.GetString()
            : null;

    private static long? So(JsonElement doi, string ten) =>
        doi.TryGetProperty(ten, out var giaTri) && giaTri.ValueKind == JsonValueKind.Number
        && giaTri.TryGetInt64(out var so)
            ? so
            : null;

    private static DateTimeOffset? ThoiDiem(JsonElement doi, string ten) =>
        doi.TryGetProperty(ten, out var giaTri) && giaTri.TryGetDateTimeOffset(out var luc) ? luc : null;
}
