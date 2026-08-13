using System.Globalization;
using System.Text;

namespace Noxh.XacMinh.Core.DanhSach;

/// <summary>
/// Nhóm đối tượng (Điều 4 Quy chế). Trong chuỗi đem băm nó là SỐ — U1 = 0 … U6 = 5 — nên chỗ này
/// là cầu nối giữa cái người ta ghi trên giấy ("U1", "1.1", "Người có công") và con số backend băm.
///
/// Phép đọc COPY NGUYÊN VĂN từ backend (<c>ImportExcelEndpoint.ParseGroup</c> +
/// <c>ExcelColumnMapper.Normalize</c>): bảng biên bản ghi tắt hay ghi nhãn đầy đủ đều là cùng một
/// nhóm, và bắt người kiểm gõ lại cho khớp mới là cách chắc chắn tạo ra kết luận KHÔNG ĐẠT oan.
/// </summary>
public static class NhomDoiTuong
{
    public const int SoNhom = 6;

    private static readonly string[] Ten =
    [
        "Người có công",
        "Thân nhân liệt sĩ",
        "Người khuyết tật",
        "Tái định cư",
        "Nữ giới",
        "Đối tượng thường",
    ];

    /// <summary>Mã nhóm dạng người đọc: 0 → <c>U1</c>.</summary>
    public static string Ma(int nhom) => $"U{nhom + 1}";

    /// <summary>Nhãn đầy đủ để đối chiếu với biên bản: 0 → <c>U1 – Người có công</c>.</summary>
    public static string Nhan(int nhom) => $"{Ma(nhom)} – {Ten[nhom]}";

    /// <summary>Đọc ô nhóm đối tượng; <c>null</c> nghĩa là không hiểu được, KHÔNG phải U6.</summary>
    public static int? Doc(string? o) => ChuanHoa(o) switch
    {
        "u1" or "1.1" or "nguoi co cong" => 0,
        "u2" or "1.2" or "than nhan liet si" => 1,
        "u3" or "1.4" or "nguoi khuyet tat" => 2,
        "u4" or "1.3" or "tai dinh cu" => 3,
        "u5" or "1.5" or "nu gioi" => 4,
        "u6" or "thuong" or "regular" => 5,
        var chuan => NhanDayDu(chuan),
    };

    /// <summary>Bảng in ra giấy ghi cả mã lẫn tên ("U1 – Người có công") — vẫn là một nhóm.</summary>
    private static int? NhanDayDu(string chuan)
    {
        for (var i = 0; i < SoNhom; i++)
        {
            var ma = ChuanHoa(Ma(i));
            var ten = ChuanHoa(Ten[i]);

            if (chuan.StartsWith(ma, StringComparison.Ordinal) && chuan.EndsWith(ten, StringComparison.Ordinal))
                return i;
        }

        return null;
    }

    /// <summary>COPY NGUYÊN VĂN từ backend: bỏ dấu tiếng Việt + chữ thường + gộp khoảng trắng.</summary>
    private static string ChuanHoa(string? s)
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
