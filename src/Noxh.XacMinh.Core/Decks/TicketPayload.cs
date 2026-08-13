namespace Noxh.XacMinh.Core.Decks;

/// <summary>
/// Đọc nội dung vé thành loại kết quả + một nhãn tiếng Việt. Danh sách dạng vé là danh sách
/// <b>đóng</b>: dạng lạ ra <see cref="TicketKind.KhongRo"/> chứ không xếp tạm vào loại gần giống —
/// lưới là thứ người dân nhìn để tin, không được vẽ thêm kết quả không có trong file.
/// </summary>
internal static class TicketPayload
{
    private const string TienToTrungCan = "TRUNG:";

    private const string TienToDuKhuyet = "DU_KHUYET:";

    public static (TicketKind Kind, string Label) Doc(string? payload)
    {
        if (payload is null) return (TicketKind.KhongRo, "chưa công bố nội dung vé");

        if (payload.StartsWith(TienToTrungCan, StringComparison.Ordinal))
            return Kem(payload[TienToTrungCan.Length..], TicketKind.Trung, "trúng căn");

        if (payload.StartsWith(TienToDuKhuyet, StringComparison.Ordinal))
            return Kem(payload[TienToDuKhuyet.Length..], TicketKind.DuKhuyet, "dự khuyết số");

        return payload switch
        {
            "TRUNG_QUYEN_MUA" => (TicketKind.Trung, "trúng quyền mua"),
            "CHO_PHAN_LOAI_DU" => (TicketKind.ChoPhanLoaiDu, "chờ phân loại căn dư"),
            "KHONG_TRUNG_UU_TIEN" => (TicketKind.KhongTrung, "không trúng suất ưu tiên"),
            "KHONG_TRUNG" => (TicketKind.KhongTrung, "không trúng"),
            _ => (TicketKind.KhongRo, "nội dung vé không thuộc dạng nào công cụ biết"),
        };
    }

    private static (TicketKind, string) Kem(string phan, TicketKind kind, string nhan) =>
        string.IsNullOrWhiteSpace(phan)
            ? (TicketKind.KhongRo, "nội dung vé không thuộc dạng nào công cụ biết")
            : (kind, $"{nhan} {MoTaGiaTri.Gon(phan)}");
}
