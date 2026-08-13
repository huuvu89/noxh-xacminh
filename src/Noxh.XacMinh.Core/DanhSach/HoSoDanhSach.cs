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

/// <summary>Bảng dán vào + khoá chỉ mục mù. Cả hai chỉ nằm trong bộ nhớ tab, không đi đâu khác.</summary>
public sealed record DanhSachDauVao(string Bang, string Khoa);
