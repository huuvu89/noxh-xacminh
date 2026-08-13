using System.Text;

namespace Noxh.XacMinh.Core.XuatKetQua;

/// <summary>
/// Rào giá trị lạ vào Markdown. Mọi giá trị trong bản xuất đều đến từ file người dùng thả vào, nên
/// một chuỗi khéo đặt có thể bẻ gãy cấu trúc file — biến hạng mục KHÔNG ĐẠT thành đoạn văn trông
/// như bình thường. Rào ở đây, một chỗ duy nhất.
/// </summary>
public static class Markdown
{
    /// <summary>Đoạn mã trong dòng: hàng rào dài hơn chuỗi dấu huyền dài nhất bên trong.</summary>
    public static string Ma(string giaTri)
    {
        var mot = MotDong(giaTri);
        var rao = new string('`', DauHuyenDaiNhat(mot) + 1);

        // CommonMark cắt bớt một khoảng trắng ở mỗi đầu, nên chèn để dấu huyền ở mép không dính rào.
        var dem = mot.StartsWith('`') || mot.EndsWith('`') ? " " : string.Empty;

        return $"{rao}{dem}{mot}{dem}{rao}";
    }

    /// <summary>Khối mã nhiều dòng (preimage) — giữ nguyên từng byte, chỉ nới hàng rào cho đủ dài.</summary>
    public static string Khoi(string noiDung, string ngonNgu)
    {
        var rao = new string('`', Math.Max(3, DauHuyenDaiNhat(noiDung) + 1));

        return $"{rao}{ngonNgu}\n{noiDung.TrimEnd('\n')}\n{rao}";
    }

    /// <summary>
    /// Ép về một dòng: giá trị nhiều dòng lọt vào giữa danh sách hay tiêu đề sẽ đẻ ra cấu trúc mới.
    /// </summary>
    public static string MotDong(string giaTri)
    {
        var ra = new StringBuilder(giaTri.Length);
        var vuaCatDong = false;

        foreach (var c in giaTri)
        {
            if (c is '\n' or '\r')
            {
                // "\r\n" là một lần xuống dòng, không phải hai.
                if (!vuaCatDong) ra.Append(' ');
                vuaCatDong = true;
                continue;
            }

            vuaCatDong = false;
            ra.Append(c);
        }

        return ra.ToString();
    }

    private static int DauHuyenDaiNhat(string noiDung)
    {
        int dai = 0, hienTai = 0;

        foreach (var c in noiDung)
        {
            hienTai = c == '`' ? hienTai + 1 : 0;
            dai = Math.Max(dai, hienTai);
        }

        return dai;
    }
}
