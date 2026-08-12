using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Noxh.XacMinh.Core.Crypto;

/// <summary>
/// COPY NGUYÊN VĂN từ backend (<c>Lottery.Api/Common/Audit/HashChainService.cs</c> —
/// <c>ComputeEntryHash</c> và <c>RoundOrdinal</c>; thứ tự vòng lấy từ
/// <c>Features/Lottery/Engine/LotteryLabels.cs</c>).
///
/// entryHash = SHA-256(prevHash ‖ applicantId ‖ deckId ‖ position(int32 big-endian) ‖ UTF-8(payload)),
/// nối byte thô, không dấu phân cách; prevHash của bước đầu tiên là chuỗi rỗng.
///
/// Hai định danh nối bằng <see cref="Guid.ToByteArray"/> — layout <b>mixed-endian</b> của .NET
/// (ba trường đầu little-endian), KHÔNG phải thứ tự byte RFC 4122. Đây là lý do công cụ này viết
/// bằng C#: dùng đúng thư viện đã sinh ra giá trị gốc thì cái bẫy đó tự biến mất.
/// </summary>
public static class DrawLogHashChain
{
    /// <summary>Tên vòng của backend — nhật ký bốc dùng đúng bộ nhãn này.</summary>
    private const string RoundA1 = "A1";

    private const string RoundA2Prefix = "A2:";

    private const string RoundBPrefix = "B:";

    private const string RoundC = "C";

    /// <summary>Byte của một định danh theo đúng layout .NET — chỗ dễ sai nhất của cả phép kiểm.</summary>
    public static byte[] DinhDanhBytes(Guid id) => id.ToByteArray();

    /// <summary>Chuỗi đem băm của một bước. <paramref name="prevHash"/> rỗng ở bước đầu tiên.</summary>
    public static byte[] Preimage(byte[] prevHash, Guid applicantId, Guid deckId, int position, string payload)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var buffer = new byte[prevHash.Length + 16 + 16 + sizeof(int) + payloadBytes.Length];
        var offset = 0;

        Buffer.BlockCopy(prevHash, 0, buffer, offset, prevHash.Length);
        offset += prevHash.Length;

        applicantId.ToByteArray().CopyTo(buffer, offset);
        offset += 16;

        deckId.ToByteArray().CopyTo(buffer, offset);
        offset += 16;

        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(offset, sizeof(int)), position);
        offset += sizeof(int);

        Buffer.BlockCopy(payloadBytes, 0, buffer, offset, payloadBytes.Length);

        return buffer;
    }

    public static byte[] EntryHash(byte[] prevHash, Guid applicantId, Guid deckId, int position, string payload) =>
        SHA256.HashData(Preimage(prevHash, applicantId, deckId, position, payload));

    /// <summary>Mã băm một bước dạng hex thường — đúng dạng đang công bố trong báo cáo minh bạch.</summary>
    public static string EntryHashHex(byte[] prevHash, Guid applicantId, Guid deckId, int position, string payload) =>
        Convert.ToHexString(EntryHash(prevHash, applicantId, deckId, position, payload)).ToLowerInvariant();

    /// <summary>Thứ tự vòng khi duyệt chuỗi: A1=0, A2=1, B=2, C=3; vòng lạ xếp cuối (vẫn tất định).</summary>
    public static int ThuTuVong(string round)
    {
        if (round == RoundA1) return 0;
        if (round.StartsWith(RoundA2Prefix, StringComparison.Ordinal)) return 1;
        if (round.StartsWith(RoundBPrefix, StringComparison.Ordinal)) return 2;
        if (round == RoundC) return 3;
        return int.MaxValue;
    }
}
