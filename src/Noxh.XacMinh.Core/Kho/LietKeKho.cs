using System.Xml;
using System.Xml.Linq;

namespace Noxh.XacMinh.Core.Kho;

/// <summary>
/// Một trang danh sách object đọc được từ kho. <see cref="DauTiepTuc"/> khác <c>null</c> nghĩa là còn
/// trang sau — chưa đọc nốt thì <b>không được</b> kết luận về khoảng trống số thứ tự lô.
/// </summary>
public sealed record KetQuaLietKe(IReadOnlyList<string> Key, string? DauTiepTuc, string? Loi);

/// <summary>
/// Bóc câu trả lời <c>ListObjectsV2</c> của kho. Kho từ chối cũng trả về XML (thẻ <c>Error</c>), nên
/// mã lỗi của kho được giữ nguyên đưa lên màn hình: "AccessDenied" nói cho người kiểm biết khoá họ
/// dán không đọc được kho này, khác hẳn với "kho rỗng".
/// </summary>
public static class LietKeKho
{
    public static KetQuaLietKe Doc(string xml)
    {
        XDocument tai;

        try
        {
            tai = XDocument.Parse(xml);
        }
        catch (XmlException ex)
        {
            return new KetQuaLietKe([], null, $"kho trả về nội dung không đọc được: {ex.Message}");
        }

        var goc = tai.Root;

        if (goc is null) return new KetQuaLietKe([], null, "kho trả về nội dung rỗng");

        if (string.Equals(goc.Name.LocalName, "Error", StringComparison.Ordinal))
        {
            var ma = LayGiaTri(goc, "Code") ?? "không rõ mã lỗi";
            var loiChiTiet = LayGiaTri(goc, "Message");

            return new KetQuaLietKe([], null,
                $"kho từ chối: {ma}" + (loiChiTiet is null ? string.Empty : $" ({loiChiTiet})"));
        }

        var key = goc.Descendants()
            .Where(e => e.Name.LocalName == "Contents")
            .Select(e => LayGiaTri(e, "Key"))
            .Where(k => !string.IsNullOrEmpty(k))
            .Select(k => k!)
            .ToList();

        var conTrangSau = string.Equals(LayGiaTri(goc, "IsTruncated"), "true", StringComparison.OrdinalIgnoreCase);
        var dauTiepTuc = LayGiaTri(goc, "NextContinuationToken");

        return new KetQuaLietKe(key, conTrangSau ? dauTiepTuc : null, null);
    }

    private static string? LayGiaTri(XElement cha, string ten) =>
        cha.Elements().FirstOrDefault(e => e.Name.LocalName == ten)?.Value;
}
