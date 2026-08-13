using System.Globalization;
using System.Text;

namespace Noxh.XacMinh.Core.DanhSach;

/// <summary>
/// COPY NGUYÊN VĂN từ backend (<c>ExcelColumnMapper.Normalize</c>): bỏ dấu tiếng Việt, về chữ
/// thường, gộp khoảng trắng lặp.
///
/// Backend dùng đúng một hàm này cho <b>cả hai</b> việc — nhận tên cột lúc nhập file Excel và đọc ô
/// nhóm đối tượng — nên ở đây cũng chỉ được có một bản. Hai bản chép rời nhau là cách chắc chắn để
/// một hôm nào đó công cụ đọc tên cột khác hệ thống mà không ai thấy.
/// </summary>
internal static class ChuanHoaTen
{
    public static string Doc(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        s = s.Trim().ToLowerInvariant().Replace('đ', 'd').Replace('Đ', 'd');
        var tach = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(tach.Length);
        foreach (var ch in tach)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);

        var chuan = sb.ToString().Normalize(NormalizationForm.FormC);

        return string.Join(' ', chuan.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
