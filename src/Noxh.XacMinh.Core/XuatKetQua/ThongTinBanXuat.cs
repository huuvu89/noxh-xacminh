namespace Noxh.XacMinh.Core.XuatKetQua;

/// <summary>
/// Một mẩu dữ liệu đầu vào đã đi vào lần kiểm này, kèm mã băm của <b>đúng byte</b> đã dùng. Người
/// thứ ba cầm bản xuất phải đối chiếu được bằng <c>sha256sum</c> trên file họ tự tải, không phải
/// tin lời bản xuất.
/// </summary>
public sealed record DauVaoDaKiem(string Ten, string MaBamSha256);

/// <summary>
/// Mọi thứ ngoài bản thân kết luận mà bản xuất phải mang theo. Lõi không có đồng hồ và không đọc
/// file: thời điểm kiểm và mã băm đầu vào do vỏ UI đưa vào, nhờ vậy bản xuất tất định và test được.
/// </summary>
public sealed record ThongTinBanXuat(
    DateTimeOffset ThoiDiemKiem,
    string PhienBanCongCu,
    IReadOnlyList<DauVaoDaKiem> DauVao,
    string? Nguon = null);
