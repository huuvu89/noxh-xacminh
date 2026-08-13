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
        projectId = MaDuAnThu,
        occurredAt = XayRaLuc,
        payload = new { applicantId = maHoSo, position = viTri },
    };

    public const string MaDuAnThu = "11111111-1111-1111-1111-111111111111";

    private static readonly DateTime XayRaLuc = new(2026, 8, 15, 7, 30, 0, DateTimeKind.Utc);

    /// <summary>Bản ghi một lượt bốc — khuôn của <c>DrawTicket.cs</c> (chỉ vé do người bấm mới lên trail).</summary>
    public static object LuotBoc(
        string maDuAn, string? vong, string? maHoSo, string? maChongPhieu, int? viTri, string? noiDungVe) => new
    {
        kind = "TICKET_DRAWN",
        projectId = maDuAn,
        occurredAt = XayRaLuc,
        payload = new
        {
            applicantId = maHoSo,
            deckId = maChongPhieu,
            round = vong,
            position = viTri,
            ticketPayload = noiDungVe,
        },
    };

    /// <summary>
    /// Bản ghi cam kết các phần bí mật — khuôn của <c>SupervisorEntropy.cs</c>. Trail chỉ mang mã băm
    /// cam kết, không bao giờ mang phần ngẫu nhiên thô (thứ đó chỉ lộ ở lúc đóng cổng).
    /// </summary>
    public static object CamKetNgauNhien(
        string maDuAn, string? camKetA, string? camKetB, string? camKetC, int lanChot = 1) => new
    {
        kind = "SECRET_COMMIT",
        projectId = maDuAn,
        occurredAt = XayRaLuc,
        payload = new
        {
            scope = "SUPERVISOR_ENTROPY",
            rServerCommitA = camKetA,
            rServerCommitB = camKetB,
            rServerCommitC = camKetC,
            refreeze = lanChot > 1,
            freezeCount = lanChot,
        },
    };

    /// <summary>Bản ghi đầu chuỗi băm của một vòng — khuôn của <c>StepChainSealer.cs</c>.</summary>
    public static object DauChuoi(string maDuAn, string? vong, int? soBuoc, string? dauChuoiHex) => new
    {
        kind = "STEPCHAIN_HEAD",
        projectId = maDuAn,
        occurredAt = XayRaLuc,
        payload = new { round = vong, stepCount = soBuoc, headHex = dauChuoiHex },
    };
}
