using System.Text.Json;
using System.Text.Json.Serialization;

namespace Noxh.XacMinh.Web.BanDung;

/// <summary>Đúng những trường mà <c>deploy/dung-ban-xuat-ban.sh</c> ghi ra <c>build-info.json</c>.</summary>
internal sealed class BanDungJson
{
    [JsonPropertyName("maCommit")] public string? MaCommit { get; init; }

    [JsonPropertyName("tenGoi")] public string? TenGoi { get; init; }

    [JsonPropertyName("maBamGoi")] public string? MaBamGoi { get; init; }

    [JsonPropertyName("linkLanDung")] public string? LinkLanDung { get; init; }

    [JsonPropertyName("thoiDiemDung")] public string? ThoiDiemDung { get; init; }
}

/// <summary>Sinh mã đọc JSON lúc biên dịch — bản publish có trimming, reflection sẽ hụt trường.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(BanDungJson))]
internal partial class BanDungJsonContext : JsonSerializerContext;

/// <summary>
/// Dấu vết của lần dựng đã sinh ra chính bản đang chạy. Người hoài nghi cần ba thứ để nối trang
/// web với mã nguồn công khai: commit nào, gói tải về băm ra gì, và lần chạy dựng nào làm ra nó.
/// Không có dấu vết thì phải hiện ra là không có — một chân trang trống trông y hệt một chân trang
/// đã đối chiếu xong.
/// </summary>
public sealed record ThongTinBanDung(
    string MaCommit,
    string TenGoi,
    string MaBamGoi,
    string LinkLanDung,
    string ThoiDiemDung)
{
    /// <summary>Chạy từ `dotnet run`, hoặc bản đem lên bằng tay: không có gì để đối chiếu.</summary>
    public static ThongTinBanDung KhongRo { get; } = new("", "", "", "", "");

    public bool CoDauVet => MaCommit.Length > 0;

    public bool CoMaBamGoi => MaBamGoi.Length > 0;

    public bool CoGoiOffline => TenGoi.Length > 0;

    public bool CoLinkLanDung => LinkLanDung.Length > 0;

    public bool CoThoiDiem => ThoiDiemDung.Length > 0;

    /// <summary>
    /// Trả <c>null</c> khi không đọc được. Máy chủ tĩnh trả HTML cho file thiếu là chuyện thường,
    /// và một ngoại lệ ở đây làm trắng cả công cụ kiểm chứng chỉ vì một dòng chân trang.
    /// </summary>
    public static ThongTinBanDung? TuJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        BanDungJson? doc;
        try
        {
            doc = JsonSerializer.Deserialize(json, BanDungJsonContext.Default.BanDungJson);
        }
        catch (JsonException)
        {
            return null;
        }

        // Không có commit thì không có gì để nối trang với mã nguồn — coi như không có dấu vết.
        if (doc is null || string.IsNullOrWhiteSpace(doc.MaCommit)) return null;

        return new ThongTinBanDung(
            doc.MaCommit.Trim(),
            doc.TenGoi?.Trim() ?? "",
            doc.MaBamGoi?.Trim() ?? "",
            doc.LinkLanDung?.Trim() ?? "",
            doc.ThoiDiemDung?.Trim() ?? "");
    }
}
