using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Dựng lô bằng chứng đúng khuôn backend đẩy lên kho (<c>TrailBatchBuilder</c>, <c>NOXH-TRAIL-v1</c>):
/// dòng đầu là <c>BATCH_HEADER</c> mang <c>(prevKey, prevSha256)</c>, các dòng sau là bản ghi, tất cả
/// nối bằng <c>\n</c> và mã hoá UTF-8 không BOM; key sort được theo thời gian và mang số thứ tự lô
/// cùng mã tiến trình.
///
/// Chép lại khuôn ở đây (thay vì tham chiếu backend) là chủ ý: công cụ kiểm phải đọc được đúng thứ
/// đang nằm trên kho, nên khi backend đổi định dạng thì chỗ lệch phải lộ ra ở đây trước.
/// </summary>
internal static class DungLoTrail
{
    public const string TienTo = "trail/";

    public static byte[] NoiDung(
        long soLo,
        string maTienTrinh,
        DateTime taoLuc,
        IReadOnlyList<object> banGhi,
        string? keyLoTruoc,
        string? shaLoTruoc)
    {
        var sb = new StringBuilder();
        sb.Append(JsonSerializer.Serialize(new
        {
            kind = "BATCH_HEADER",
            version = "NOXH-TRAIL-v1",
            batchNo = soLo,
            instanceId = maTienTrinh,
            createdAt = taoLuc,
            recordCount = banGhi.Count,
            prevKey = keyLoTruoc,
            prevSha256 = shaLoTruoc,
        })).Append('\n');

        foreach (var r in banGhi) sb.Append(JsonSerializer.Serialize(r)).Append('\n');

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public static string Key(long soLo, string maTienTrinh, DateTime taoLuc) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{TienTo}{taoLuc:yyyy/MM/dd}/{taoLuc:HHmmssfff}Z-b{soLo:D6}-{maTienTrinh}.jsonl");

    public static string Sha256Hex(byte[] noiDung) =>
        Convert.ToHexString(SHA256.HashData(noiDung)).ToLowerInvariant();

    public static object VeDaBoc(string maHoSo, int viTri) => new
    {
        kind = "TICKET_DRAWN",
        projectId = "11111111-1111-1111-1111-111111111111",
        occurredAt = new DateTime(2026, 8, 15, 7, 30, 0, DateTimeKind.Utc),
        payload = new { applicantId = maHoSo, position = viTri },
    };
}
