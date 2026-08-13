namespace Noxh.XacMinh.Core.DanhSach;

/// <summary>
/// Một dòng của bảng danh sách hồ sơ đã khoá, đọc từ bảng người kiểm dán vào. Bốn trường này —
/// và chỉ bốn trường này — là thứ backend đem băm thành <c>ListHash</c>; <see cref="Nhom"/> giữ
/// dạng SỐ (0..5 ứng với U1..U6) vì chuỗi đem băm mang số chứ không mang chữ.
/// </summary>
/// <param name="MaHoSo">Mã hồ sơ, giữ nguyên văn.</param>
/// <param name="HoTen">Họ tên, giữ nguyên văn — kể cả khoảng trắng thừa; công cụ kiểm KHÔNG tự sửa.</param>
/// <param name="SoDinhDanh">Số định danh (CCCD) — chỉ dùng để tính chỉ mục mù, không đi vào mã băm.</param>
/// <param name="Nhom">Nhóm đối tượng đã đọc thành số: 0 = U1 … 5 = U6.</param>
public sealed record HoSoDanhSach(string MaHoSo, string HoTen, string SoDinhDanh, int Nhom);

/// <summary>
/// Bảng danh sách vào công cụ bằng đường nào. Hai đường, một phép kiểm duy nhất phía sau — đường
/// dán là lối thoát khi gặp file lạ, không phải đường phụ bị bỏ quên.
/// </summary>
public abstract record NguonBang
{
    private NguonBang() { }

    /// <summary>Bảng người kiểm dán vào (ngăn bằng tab hoặc kiểu <c>| … |</c>).</summary>
    public sealed record Dan(string NoiDung) : NguonBang;

    /// <summary>File Excel gốc thả thẳng vào; <paramref name="TenFile"/> chỉ để nói ra đang đọc file nào.</summary>
    public sealed record Excel(string TenFile, byte[] NoiDung) : NguonBang;

    /// <summary>Nguồn bảng, gọn đủ để đưa vào câu giải thích của hạng mục kiểm.</summary>
    public string MoTa => this switch
    {
        Dan => "bảng đã dán",
        Excel e => $"file Excel «{MoTaGiaTri.Gon(e.TenFile)}»",
        _ => "bảng danh sách",
    };
}

/// <summary>Bảng danh sách + khoá chỉ mục mù. Cả hai chỉ nằm trong bộ nhớ tab, không đi đâu khác.</summary>
public sealed record DanhSachDauVao(NguonBang Nguon, string Khoa)
{
    public DanhSachDauVao(string bang, string khoa) : this(new NguonBang.Dan(bang), khoa) { }
}
