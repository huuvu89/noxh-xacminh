using Noxh.XacMinh.Core.Decks;

namespace Noxh.XacMinh.Web.HienThi;

/// <summary>
/// Một chỗ duy nhất ánh xạ loại kết quả của lá vé sang lớp CSS và nhãn chữ. Ô phiếu luôn đi kèm
/// nhãn chữ (chú giải, mô tả khi chạm) chứ không chỉ có màu — người mù màu vẫn phải đọc được lưới.
/// </summary>
public static class LopLoaiVe
{
    public static string Cua(TicketKind loai) => loai switch
    {
        TicketKind.Trung => "loai-trung",
        TicketKind.ChoPhanLoaiDu => "loai-cho-phan-loai-du",
        TicketKind.DuKhuyet => "loai-du-khuyet",
        TicketKind.KhongTrung => "loai-khong-trung",
        _ => "loai-khong-ro",
    };

    public static string Nhan(TicketKind loai) => loai switch
    {
        TicketKind.Trung => "Trúng",
        TicketKind.ChoPhanLoaiDu => "Chờ phân loại căn dư",
        TicketKind.DuKhuyet => "Dự khuyết",
        TicketKind.KhongTrung => "Không trúng",
        _ => "Không rõ loại vé",
    };
}
