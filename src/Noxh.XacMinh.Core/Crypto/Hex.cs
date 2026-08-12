using System.Security.Cryptography;

namespace Noxh.XacMinh.Core.Crypto;

/// <summary>
/// Hex của bên kiểm: <see cref="Sha256Hex"/> copy nguyên văn từ backend
/// (<c>CryptoHelper.Sha256Hex</c>) vì nó sinh ra chính chuỗi đang được công bố; phần đọc hex thì
/// KHOAN DUNG có chủ ý — file người dùng thả vào có thể cụt, viết hoa, hoặc không phải hex, và
/// những ca đó phải ra KHÔNG KIỂM ĐƯỢC chứ không phải một ngoại lệ làm trắng trang.
/// </summary>
public static class Hex
{
    /// <summary>COPY NGUYÊN VĂN từ backend: SHA-256 của bytes, hex thường.</summary>
    public static string Sha256Hex(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    /// <summary>Đọc chuỗi hex đã công bố; <c>null</c> nếu rỗng hoặc không phải hex hợp lệ.</summary>
    public static byte[]? Doc(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;

        var text = hex.Trim();
        try
        {
            return Convert.FromHexString(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Dạng chuẩn hoá để đối chiếu và để hiện ở chế độ chuyên sâu.</summary>
    public static string? ChuanHoa(string? hex) =>
        string.IsNullOrWhiteSpace(hex) ? null : hex.Trim().ToLowerInvariant();
}
