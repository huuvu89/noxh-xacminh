namespace Noxh.XacMinh.Core.Decks;

/// <summary>
/// Loại kết quả của một lá vé. <see cref="KhongRo"/> dành cho nội dung vé công cụ không nhận ra:
/// đoán bừa nó là "không trúng" thì lưới vẽ ra một cuộc bốc thăm không có thật.
/// </summary>
public enum TicketKind
{
    Trung,
    ChoPhanLoaiDu,
    DuKhuyet,
    KhongTrung,
    KhongRo,
}

/// <summary>Bộ lọc lưới — dân số ~900 ô, không lọc thì tìm bằng mắt không nổi.</summary>
public enum DeckFilter
{
    TatCa,
    ChiTrung,
    ChiMayBoc,
    ChiChuaBoc,
}

/// <summary>
/// Một lượt bốc đã nhận ô phiếu này. <see cref="AutoDrawn"/> rỗng nghĩa là báo cáo không công bố
/// ai bấm — không phải "người tự bấm".
/// </summary>
public sealed record CellDraw(string? ApplicantId, bool? AutoDrawn);

/// <summary>
/// Một ô phiếu. <see cref="Draws"/> giữ <b>mọi</b> lượt bốc nhận ô này: bình thường nhiều nhất một
/// lượt, nhưng file bị chèn thêm lượt thì lưới phải cho thấy chỗ đó, không được giấu bớt.
/// </summary>
public sealed record DeckCell(
    int Position,
    string? Payload,
    TicketKind Kind,
    string Label,
    IReadOnlyList<CellDraw> Draws)
{
    public bool Drawn => Draws.Count > 0;

    public bool AutoDrawn => Draws.Any(d => d.AutoDrawn == true);
}

/// <summary>
/// Số liệu tóm tắt của một chồng phiếu. Ba trường liên quan tới lượt bốc là <c>null</c> khi báo cáo
/// không ghép được nhật ký bốc cho chồng phiếu này — "không biết" chứ không phải "bằng 0".
/// </summary>
public sealed record DeckSummary(int Size, int WonCount, int? ManualDraws, int? AutoDraws, int? UndrawnCells)
{
    public bool HasDrawLog => UndrawnCells is not null;
}

/// <summary>Lưới ô phiếu của một chồng phiếu đã công bố, kèm số liệu tóm tắt và phép lọc/tra cứu.</summary>
public sealed record DeckGrid(string? Round, string? DeckHash, IReadOnlyList<DeckCell> Cells, DeckSummary Summary)
{
    /// <summary>
    /// Lọc theo loại vé/lượt bốc rồi lọc tiếp theo mã hồ sơ giả (chuỗi con, không phân biệt hoa
    /// thường). Chưa ghép được nhật ký bốc thì hai bộ lọc theo lượt bốc trả rỗng: nhãn "chưa ai bốc"
    /// cho ô mà công cụ không biết gì là nói dối.
    /// </summary>
    public IReadOnlyList<DeckCell> Filter(DeckFilter filter, string? maHoSo)
    {
        var theoLoai = filter switch
        {
            DeckFilter.ChiTrung => Cells.Where(o => o.Kind == TicketKind.Trung),
            DeckFilter.ChiMayBoc => Summary.HasDrawLog ? Cells.Where(o => o.AutoDrawn) : [],
            DeckFilter.ChiChuaBoc => Summary.HasDrawLog ? Cells.Where(o => !o.Drawn) : [],
            _ => Cells,
        };

        var tim = maHoSo?.Trim();

        return (string.IsNullOrEmpty(tim)
                ? theoLoai
                : theoLoai.Where(o => o.Draws.Any(d =>
                    d.ApplicantId?.Contains(tim, StringComparison.OrdinalIgnoreCase) == true)))
            .ToList();
    }
}
