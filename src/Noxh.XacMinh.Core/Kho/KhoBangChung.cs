namespace Noxh.XacMinh.Core.Kho;

/// <summary>
/// Những gì vỏ UI đọc được từ kho bằng chứng, đưa vào lõi dưới dạng dữ liệu. Lõi không biết mạng là
/// gì, nên "hỏi không được" cũng phải là một dữ kiện (<see cref="Loi"/>) chứ không phải một ngoại lệ
/// bị nuốt: kho im lặng mà công cụ báo xanh là kiểu nói dối tệ nhất.
///
/// <see cref="DaLietKeHet"/> sai nghĩa là danh sách object còn dở (còn trang chưa đọc, đọc lỗi giữa
/// chừng) — lúc đó khoảng trống số thứ tự lô chưa nói lên điều gì.
/// </summary>
public sealed record KhoBangChung(
    CheDoDocKho CheDo,
    IReadOnlyList<LoBangChung> Lo,
    string? MoTaNguon = null,
    string? Loi = null,
    bool DaLietKeHet = true)
{
    public static KhoBangChung Doc(
        CheDoDocKho cheDo,
        IReadOnlyList<DoiTuongKho> doiTuong,
        string? moTaNguon = null,
        bool daLietKeHet = true) =>
        new(cheDo,
            [.. doiTuong.OrderBy(d => d.Key, StringComparer.Ordinal).Select(DocLoBangChung.Doc)],
            moTaNguon,
            null,
            daLietKeHet);

    public static KhoBangChung Hong(CheDoDocKho cheDo, string loi, string? moTaNguon = null) =>
        new(cheDo, [], moTaNguon, loi, false);

    public string MoTaCheDo => CheDo switch
    {
        CheDoDocKho.KhoaChiDoc => "đọc bằng khoá chỉ-đọc người kiểm dán vào",
        CheDoDocKho.GoiNhapTay =>
            "nạp từ gói đã tải sẵn bằng script — KHÔNG phải trình duyệt này đọc thẳng kho, nên "
            + "\"các lô này đang nằm trên kho\" là điều công cụ không tự chứng kiến được, nó chỉ băm lại "
            + "đúng byte trong gói",
        _ => "đọc ẩn danh (kho đã mở công khai)",
    };
}
