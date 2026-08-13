using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Units;

namespace Noxh.XacMinh.Core.Decks;

/// <summary>
/// Suy ra <b>những căn đã được phân trước vòng bốc thẳng</b> từ bảng kết quả đã công bố. Quỹ căn còn
/// dư của vòng bốc thẳng không nằm sẵn trong báo cáo minh bạch, nên đây là một <b>suy diễn của công
/// cụ</b>, không phải dữ liệu ban tổ chức công bố — mọi chỗ dùng nó phải nói rõ điều đó.
///
/// Không suy từ vé <c>TRUNG:</c> của chồng phiếu vòng ưu tiên: ô phiếu không ai bốc thì căn trên vé
/// đó vẫn còn dư, và máy gom phân căn cho người giữ vé chờ mà chẳng qua chồng phiếu nào. Bảng kết
/// quả là chỗ duy nhất nói căn nào <b>thật sự</b> đã về tay ai.
/// </summary>
internal static class QuyCanConDu
{
    /// <summary>
    /// Tầng kết quả công cụ biết — danh sách <b>đóng</b>. Tầng lạ nghĩa là báo cáo có vòng công cụ
    /// chưa biết, xếp bừa nó vào trước hay sau vòng bốc thẳng đều là tự bịa ra quỹ căn.
    /// </summary>
    private static readonly IReadOnlyList<string> TangDaBiet =
        ["UuTienTrucTiep", "UuTienBocXam", "Regular", "Leftover", "LeftoverWaitlist"];

    /// <summary>Hai tầng của vòng phân căn ưu tiên (bốc xăm + phân trực tiếp) — đều trước vòng bốc thẳng.</summary>
    private static readonly IReadOnlyList<string> TangTruocVongBocThang = ["UuTienTrucTiep", "UuTienBocXam"];

    /// <summary>
    /// Căn đã phân trước vòng bốc thẳng, sắp theo mã căn. <c>MauThuan</c> có giá trị nghĩa là suy diễn
    /// <b>không nhất quán</b> — khi đó không được kết luận đạt hay không đạt, chỉ được nói chưa kiểm
    /// được kèm chính mâu thuẫn đó.
    /// </summary>
    public static (IReadOnlyList<string>? DaPhan, string? MauThuan) DaPhanTruocVongBocThang(
        TransparencyReport report, UnitCatalog danhMuc)
    {
        if (report.Results?.Rows is not { } dong)
            return (null, "Báo cáo không công bố bảng kết quả chung cuộc, mà quỹ căn còn dư của vòng này chỉ "
                + "suy ra được từ những căn đã phân ở vòng trước — không có bảng kết quả thì không suy được "
                + "quỹ căn còn dư nào để dựng lại chồng phiếu.");

        if (dong.Any(d => d is null))
            return (null, "Bảng kết quả có dòng rỗng, nên công cụ không đọc đủ những căn đã phân ở vòng trước "
                + "để suy ra quỹ căn còn dư — suy trên một bảng thủng là tự bịa ra quỹ căn.");

        var coCan = dong.Where(d => !string.IsNullOrWhiteSpace(d!.UnitCode)).Select(d => d!).ToList();

        if (coCan.FirstOrDefault(d => !TangDaBiet.Contains(d.Tier ?? string.Empty)) is { } tangLa)
            return (null, $"Bảng kết quả có dòng mang tầng '{MoTaGiaTri.Gon(tangLa.Tier ?? "(trống)")}' — tầng "
                + "này không thuộc danh sách tầng công cụ biết, nên không biết căn của nó được phân trước hay "
                + "sau vòng bốc thẳng, tức là không suy được quỹ căn còn dư.");

        if (coCan.GroupBy(d => d.UnitCode!, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1) is { } trung)
            return (null, $"Bảng kết quả phân căn '{MoTaGiaTri.Gon(trung.Key)}' cho hai hồ sơ trở lên, nên suy "
                + "diễn quỹ căn còn dư mâu thuẫn ngay từ đầu vào — có thể là kết quả đã huỷ rồi phân lại, có "
                + "thể là bảng kết quả bị sửa. Công cụ không phân biệt được nên không kết luận.");

        // Kết quả đã huỷ (cancelledAt) vẫn tính là đã phân: huỷ là việc xảy ra SAU lễ, còn thứ đang
        // dựng lại là chồng phiếu niêm phong lúc lễ diễn ra.
        var daPhan = coCan
            .Where(d => TangTruocVongBocThang.Contains(d.Tier!))
            .Select(d => d.UnitCode!)
            .Order(StringComparer.Ordinal)
            .ToList();

        var coTrongDanhMuc = danhMuc.Types.SelectMany(t => t.UnitCodes).ToHashSet(StringComparer.Ordinal);
        if (daPhan.FirstOrDefault(can => !coTrongDanhMuc.Contains(can)) is { } lac)
            return (null, $"Bảng kết quả nói căn '{MoTaGiaTri.Gon(lac)}' đã được phân ở vòng ưu tiên, mà danh "
                + "mục căn đang dùng không có căn đó — danh mục này không phải danh mục của dự án đang kiểm, "
                + "nên trừ ra không còn đúng quỹ căn còn dư. Nạp đúng file danh mục căn rồi kiểm lại.");

        return (daPhan, null);
    }
}
