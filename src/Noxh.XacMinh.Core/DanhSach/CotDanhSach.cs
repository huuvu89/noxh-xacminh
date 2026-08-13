namespace Noxh.XacMinh.Core.DanhSach;

/// <summary>
/// Nhận cột của file Excel gốc <b>theo tên tiêu đề</b> — COPY từ backend
/// (<c>ExcelColumnMapper.Aliases</c>), nên thứ tự cột trong file không quan trọng và file có thêm
/// cột lạ vẫn đọc được. Đúng chỗ này quyết định công cụ đọc file <i>giống</i> hay <i>khác</i> cách
/// hệ thống đã đọc lúc nhập danh sách; khác một bí danh là mã băm lệch mà không ô nào sai.
/// </summary>
public static class CotDanhSach
{
    public enum Cot { MaHoSo, HoTen, SoDinhDanh, Nhom, SoDienThoai }

    /// <summary>
    /// Bí danh backend nhận, cộng thêm vài cách gọi của chính biên bản giám sát (đánh dấu bên dưới).
    /// Chỉ được <b>nới rộng</b> chỗ nhận tên cột, không bao giờ đổi giá trị đọc ra — nhận rộng thì
    /// cùng lắm là đọc được một file hệ thống sẽ từ chối, còn nhận hẹp là bắt người kiểm sửa tiêu đề
    /// file gốc, mà sửa file gốc thì hết còn là file gốc.
    /// </summary>
    private static readonly (Cot Cot, string Ten, string[] BiDanh)[] Bang =
    [
        (Cot.MaHoSo, "mã hồ sơ", ["ma ho so", "maho so", "so ho so", "ma hs"]),
        (Cot.HoTen, "họ tên", ["ho ten", "ho va ten", "hovaten", "ten"]),
        (Cot.SoDinhDanh, "số định danh (CCCD)",
            ["cccd", "so cccd", "cmnd", "cccd/cmnd", "can cuoc",
                // Thêm: biên bản của tổ giám sát gọi cột này là "số định danh".
                "so dinh danh", "dinh danh", "so dinh danh ca nhan"]),
        (Cot.Nhom, "nhóm đối tượng", ["nhom", "nhom doi tuong", "doi tuong"]),
        (Cot.SoDienThoai, "số điện thoại", ["sdt", "so dien thoai", "dien thoai", "phone", "so dt"]),
    ];

    /// <summary>
    /// Bốn cột mã băm danh sách cần. Hệ thống lúc nhập còn đòi số điện thoại và loại căn hộ, nhưng
    /// hai cột đó không đi vào mã băm — đòi thêm ở đây chỉ tạo ra lời từ chối vô ích.
    /// </summary>
    private static readonly Cot[] BatBuoc = [Cot.MaHoSo, Cot.HoTen, Cot.SoDinhDanh, Cot.Nhom];

    /// <summary>Tên cột cho người đọc, dùng khi phải nói ra thiếu cột nào.</summary>
    public static string Ten(Cot cot) => Bang.First(b => b.Cot == cot).Ten;

    /// <summary>
    /// Đọc dòng tiêu đề → cột nào nằm ở chỉ số nào (đánh số từ 0), kèm danh sách cột bắt buộc còn
    /// thiếu. Trùng tên thì cột bên phải thắng, đúng như backend.
    /// </summary>
    public static (IReadOnlyDictionary<Cot, int> Map, IReadOnlyList<string> Thieu) DocTieuDe(
        IReadOnlyList<string> tieuDe)
    {
        var theoTen = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < tieuDe.Count; i++)
        {
            var ten = ChuanHoaTen.Doc(tieuDe[i]);

            if (ten.Length > 0) theoTen[ten] = i;
        }

        var map = new Dictionary<Cot, int>();

        foreach (var (cot, _, biDanh) in Bang)
            foreach (var ten in biDanh)
                if (theoTen.TryGetValue(ten, out var i))
                {
                    map[cot] = i;
                    break;
                }

        var thieu = BatBuoc.Where(c => !map.ContainsKey(c)).Select(Ten).ToList();

        return (map, thieu);
    }
}
