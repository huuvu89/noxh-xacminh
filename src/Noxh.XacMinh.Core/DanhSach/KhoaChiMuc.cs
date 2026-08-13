using System.Text;

namespace Noxh.XacMinh.Core.DanhSach;

/// <summary>
/// Khoá chỉ mục mù (<c>K_idx</c>) người kiểm dán vào. Phép đọc COPY NGUYÊN VĂN từ backend
/// (<c>EncryptionKeyProvider.ParseKey</c>): 64 ký tự hex là khoá thật 32 byte, chuỗi khác được đọc
/// thẳng dạng UTF-8 rồi cắt/đệm về 32 byte — đúng cách backend rơi về khoá dev. Viết lại theo trí
/// nhớ ở đây thì công cụ sẽ ra một mã băm khác và đổ lỗi cho danh sách.
/// </summary>
public static class KhoaChiMuc
{
    private const int SoByte = 32;

    /// <summary>Khoá thật là 64 ký tự hex — dùng để cảnh báo khi người kiểm dán nhầm thứ khác.</summary>
    public static bool LaKhoaHex(string? khoa)
    {
        var text = khoa?.Trim();

        if (string.IsNullOrEmpty(text) || text.Length != SoByte * 2) return false;

        try
        {
            Convert.FromHexString(text);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Trả <c>null</c> khi chưa dán gì — chưa có khoá là thiếu dữ liệu, không phải lỗi.</summary>
    public static byte[]? Doc(string? khoa)
    {
        if (string.IsNullOrWhiteSpace(khoa)) return null;

        var text = khoa.Trim();

        if (LaKhoaHex(text)) return Convert.FromHexString(text);

        var bytes = Encoding.UTF8.GetBytes(text);
        Array.Resize(ref bytes, SoByte);

        return bytes;
    }
}
