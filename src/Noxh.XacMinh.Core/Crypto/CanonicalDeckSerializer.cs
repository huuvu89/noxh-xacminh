using System.Security.Cryptography;
using System.Text;

namespace Noxh.XacMinh.Core.Crypto;

/// <summary>
/// COPY NGUYÊN VĂN từ backend (<c>Lottery.Api/Common/Crypto/CanonicalDeckSerializer.cs</c>).
/// Không viết lại theo trí nhớ, không "cải tiến": lệch một byte là công cụ này báo sai cả buổi lễ.
/// Định dạng cố định, không phụ thuộc culture/locale: mỗi vé một dòng "{position}\t{payload}\n",
/// UTF-8 không BOM, position là chỉ số 0-based theo đúng thứ tự deck sau khi shuffle.
/// DeckHash = SHA-256(bytes) hex thường.
/// </summary>
public static class CanonicalDeckSerializer
{
    public static byte[] Serialize(IReadOnlyList<string> orderedTickets)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < orderedTickets.Count; i++)
        {
            sb.Append(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            sb.Append('\t');
            sb.Append(orderedTickets[i]);
            sb.Append('\n');
        }
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(sb.ToString());
    }

    /// <summary>SHA-256 hex (thường) của serialize(deck) — chính là DeckHash công bố trước khi mở.</summary>
    public static string Hash(IReadOnlyList<string> orderedTickets) =>
        Convert.ToHexString(SHA256.HashData(Serialize(orderedTickets))).ToLowerInvariant();

    /// <summary>Chuỗi canonical dạng chữ — dùng cho chế độ chuyên sâu hiển thị preimage.</summary>
    public static string CanonicalText(IReadOnlyList<string> orderedTickets) =>
        Encoding.UTF8.GetString(Serialize(orderedTickets));
}
