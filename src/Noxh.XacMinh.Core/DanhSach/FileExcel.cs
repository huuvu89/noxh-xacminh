using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Noxh.XacMinh.Core.DanhSach;

/// <summary>Một dòng CÓ dữ liệu của trang tính. <see cref="O"/> đánh số từ cột A = 0.</summary>
public sealed record DongExcel(int SoDong, IReadOnlyList<string> O)
{
    /// <summary>Ô ngoài vùng đã ghi là ô rỗng, không phải lỗi — bảng tính thưa là chuyện thường.</summary>
    public string LayO(int cot) => cot >= 0 && cot < O.Count ? O[cot] : string.Empty;
}

/// <summary>
/// Kết quả đọc file. <see cref="Loi"/> khác <c>null</c> nghĩa là file không đọc được — chưa kiểm
/// được, không phải danh sách sai.
/// </summary>
public sealed record KetQuaDocFileExcel(IReadOnlyList<DongExcel> Dong, string? TenTrang, string? Loi);

/// <summary>
/// Đọc file <c>.xlsx</c> thành lưới chuỗi, <b>mô phỏng đúng cách backend đọc lúc nhập danh sách</b>
/// (<c>ImportExcelEndpoint</c> dùng ClosedXML 0.102.3): trang tính đầu tiên, các dòng có dữ liệu,
/// mỗi ô lấy đúng chuỗi <c>IXLCell.GetString()</c> trả về.
///
/// Vì sao tự đọc thay vì nhúng ClosedXML: công cụ này là một trang WebAssembly tĩnh người kiểm tải
/// về, và ClosedXML kéo theo cả OpenXml + XLParser/Irony — vừa nặng vài MB, vừa dựa vào reflection
/// nên bản publish có trimming rất dễ hỏng đúng lúc chạy thật. Đổi lại phải trả một cái giá, và giá
/// đó được trả bằng test: <c>FileExcelTests</c> so từng ô với chính ClosedXML 0.102.3 (chỉ là phụ
/// thuộc của dự án test), nên lệch cách đọc là test đỏ chứ không phải kết luận sai trên hội trường.
///
/// Hai chỗ cố tình không mô phỏng, vì không xảy ra ở bốn cột hạng mục này cần và mô phỏng nửa vời
/// còn tệ hơn:
///  · Ô định dạng <b>ngày tháng</b> đọc ra số sê-ri của Excel, không ra chuỗi ngày. Mã hồ sơ, họ
///    tên, số định danh, nhóm đối tượng không ai ghi kiểu ngày; nếu có thì mã băm lệch và nói lệch,
///    chứ công cụ không đoán hộ.
///  · Số dùng quy tắc của <b>ngôn ngữ bất biến</b> (ClosedXML dùng ngôn ngữ của máy). Cùng một file
///    phải ra cùng một mã băm dù mở trên máy nào — cùng lý do <c>MaBamDanhSach</c> sắp theo ngôn
///    ngữ bất biến.
/// </summary>
public static class FileExcel
{
    /// <summary>Chặn file cố tình phình khi giải nén; chạm trần là báo ra, không cắt lặng lẽ.</summary>
    private const int GioiHanDong = 200_000;

    private const string NsQuanHe = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>Chữ ký OLE2 của <c>.xls</c> đời cũ — nhầm định dạng là lỗi hay gặp nhất.</summary>
    private static readonly byte[] ChuKyXlsCu = [0xD0, 0xCF, 0x11, 0xE0];

    public static KetQuaDocFileExcel Doc(byte[] noiDung)
    {
        if (noiDung.Length == 0) return Hong("file rỗng");

        if (noiDung.Length >= ChuKyXlsCu.Length && noiDung.Take(ChuKyXlsCu.Length).SequenceEqual(ChuKyXlsCu))
            return Hong("đây là file Excel định dạng .xls đời cũ (nhị phân), không phải .xlsx — "
                        + "hãy mở bằng Excel rồi «Lưu thành» định dạng .xlsx và thả lại");

        ZipArchive zip;

        try
        {
            zip = new ZipArchive(new MemoryStream(noiDung, writable: false), ZipArchiveMode.Read);
        }
        catch (InvalidDataException)
        {
            return Hong("không mở được như file .xlsx (file .xlsx thực chất là một gói nén, file này "
                        + "không phải) — kiểm tra lại xem có đúng file gốc không");
        }

        using (zip)
        {
            try
            {
                return DocGoi(zip);
            }
            catch (LoiDocFile ex)
            {
                return Hong(ex.Message);
            }
            catch (XmlException ex)
            {
                return Hong($"nội dung XML bên trong file hỏng ({ex.Message})");
            }
            catch (InvalidDataException ex)
            {
                return Hong($"gói nén bên trong file hỏng ({ex.Message})");
            }
        }
    }

    private static KetQuaDocFileExcel DocGoi(ZipArchive zip)
    {
        var duongWorkbook = DuongQuanHe(zip, "_rels/.rels", string.Empty, "officeDocument")
                            ?? "xl/workbook.xml";
        var thuMucWorkbook = ThuMucCua(duongWorkbook);
        var relsWorkbook = $"{thuMucWorkbook}_rels/{TenTepCua(duongWorkbook)}.rels";

        var workbook = TaiXml(zip, duongWorkbook)
                       ?? throw new LoiDocFile("không tìm thấy phần workbook bên trong file");

        var trang = workbook.Descendants().FirstOrDefault(e => e.Name.LocalName == "sheet")
                    ?? throw new LoiDocFile("file không có trang tính nào");

        var tenTrang = (string?)trang.Attribute("name");
        var maQuanHe = (string?)trang.Attribute(XName.Get("id", NsQuanHe));

        var duongTrang = maQuanHe is null
            ? null
            : DuongQuanHeTheoMa(zip, relsWorkbook, thuMucWorkbook, maQuanHe);

        duongTrang ??= $"{thuMucWorkbook}worksheets/sheet1.xml";

        var chuoiChung = DocChuoiChung(zip,
            DuongQuanHe(zip, relsWorkbook, thuMucWorkbook, "sharedStrings") ?? $"{thuMucWorkbook}sharedStrings.xml");

        var muc = TimMuc(zip, duongTrang)
                  ?? throw new LoiDocFile($"không tìm thấy trang tính «{tenTrang ?? duongTrang}» bên trong file");

        return new KetQuaDocFileExcel(DocTrang(muc, chuoiChung), tenTrang, Loi: null);
    }

    private static List<DongExcel> DocTrang(ZipArchiveEntry muc, IReadOnlyList<string> chuoiChung)
    {
        var ketQua = new List<DongExcel>();

        using var stream = muc.Open();
        using var doc = TaoBoDoc(stream);

        var soDongKe = 1;

        while (!doc.EOF)
        {
            if (doc.NodeType != XmlNodeType.Element || doc.LocalName != "row")
            {
                doc.Read();
                continue;
            }

            var dong = (XElement)XNode.ReadFrom(doc);
            var soDong = int.TryParse((string?)dong.Attribute("r"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var r)
                ? r
                : soDongKe;

            soDongKe = soDong + 1;

            var o = DocDong(dong, chuoiChung);

            // "Dòng có dữ liệu" theo đúng nghĩa RowsUsed() của ClosedXML: có ít nhất một ô còn nội
            // dung. Ô chỉ chứa khoảng trắng VẪN tính là có dữ liệu — bỏ nó đi là công cụ tự dọn dữ
            // liệu, mà hệ thống lúc nhập cũng không dọn.
            if (o.Any(giaTri => giaTri.Length > 0)) ketQua.Add(new DongExcel(soDong, o));

            if (ketQua.Count > GioiHanDong)
                throw new LoiDocFile($"trang tính có hơn {GioiHanDong} dòng dữ liệu — quá lớn so với "
                                     + "một danh sách hồ sơ, công cụ dừng lại thay vì treo tab");
        }

        return ketQua;
    }

    private static List<string> DocDong(XElement dong, IReadOnlyList<string> chuoiChung)
    {
        var o = new List<string>();
        var cotKe = 0;

        foreach (var c in dong.Elements().Where(e => e.Name.LocalName == "c"))
        {
            var cot = ChiSoCot((string?)c.Attribute("r")) ?? cotKe;
            cotKe = cot + 1;

            while (o.Count <= cot) o.Add(string.Empty);

            o[cot] = GiaTriO(c, chuoiChung);
        }

        return o;
    }

    /// <summary>Đúng chuỗi <c>IXLCell.GetString()</c> trả về cho từng kiểu ô.</summary>
    private static string GiaTriO(XElement c, IReadOnlyList<string> chuoiChung)
    {
        var kieu = (string?)c.Attribute("t") ?? "n";

        if (kieu == "inlineStr")
            return GhepChuoi(c.Elements().FirstOrDefault(e => e.Name.LocalName == "is"));

        var v = c.Elements().FirstOrDefault(e => e.Name.LocalName == "v")?.Value;

        if (v is null) return string.Empty;

        return kieu switch
        {
            "s" => int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
                   && i >= 0 && i < chuoiChung.Count
                ? chuoiChung[i]
                : string.Empty,
            "str" => v,                                  // chuỗi công thức đã tính sẵn
            "b" => v == "0" ? "FALSE" : "TRUE",
            "e" => v,                                    // ô lỗi: "#DIV/0!"…
            _ => SoRaChuoi(v),
        };
    }

    private static string SoRaChuoi(string v) =>
        double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var so)
            ? so.ToString(CultureInfo.InvariantCulture)
            : v;

    private static List<string> DocChuoiChung(ZipArchive zip, string duong)
    {
        var ketQua = new List<string>();
        var muc = TimMuc(zip, duong);

        if (muc is null) return ketQua;

        using var stream = muc.Open();
        using var doc = TaoBoDoc(stream);

        while (!doc.EOF)
        {
            if (doc.NodeType == XmlNodeType.Element && doc.LocalName == "si")
                ketQua.Add(GhepChuoi((XElement)XNode.ReadFrom(doc)));
            else
                doc.Read();
        }

        return ketQua;
    }

    /// <summary>
    /// Chuỗi của một <c>si</c>/<c>is</c>: nối mọi <c>t</c> bên trong (chuỗi có định dạng bị Excel cắt
    /// thành nhiều đoạn <c>r</c>). Bỏ <c>rPh</c> — đó là phần phiên âm, không phải nội dung ô.
    /// </summary>
    private static string GhepChuoi(XElement? goc) =>
        goc is null
            ? string.Empty
            : string.Concat(goc.DescendantsAndSelf()
                .Where(e => e.Name.LocalName == "t" && e.Ancestors().All(a => a.Name.LocalName != "rPh"))
                .Select(e => e.Value));

    /// <summary>«AB12» → 27. <c>null</c> khi ô không ghi địa chỉ (hiếm, nhưng đúng chuẩn).</summary>
    private static int? ChiSoCot(string? diaChi)
    {
        if (string.IsNullOrEmpty(diaChi)) return null;

        var chiSo = 0;

        foreach (var ch in diaChi)
        {
            if (ch is >= 'A' and <= 'Z') chiSo = chiSo * 26 + (ch - 'A' + 1);
            else if (ch is >= 'a' and <= 'z') chiSo = chiSo * 26 + (ch - 'a' + 1);
            else break;
        }

        return chiSo > 0 ? chiSo - 1 : null;
    }

    private static string? DuongQuanHe(ZipArchive zip, string duongRels, string thuMuc, string duoiKieu) =>
        TaiXml(zip, duongRels)
            ?.Descendants().Where(e => e.Name.LocalName == "Relationship")
            .Where(e => ((string?)e.Attribute("Type"))?.EndsWith('/' + duoiKieu, StringComparison.Ordinal) == true)
            .Select(e => GhepDuong(thuMuc, (string?)e.Attribute("Target")))
            .FirstOrDefault(d => d is not null);

    private static string? DuongQuanHeTheoMa(ZipArchive zip, string duongRels, string thuMuc, string ma) =>
        TaiXml(zip, duongRels)
            ?.Descendants().Where(e => e.Name.LocalName == "Relationship")
            .Where(e => (string?)e.Attribute("Id") == ma)
            .Select(e => GhepDuong(thuMuc, (string?)e.Attribute("Target")))
            .FirstOrDefault(d => d is not null);

    private static string? GhepDuong(string thuMuc, string? dich)
    {
        if (string.IsNullOrEmpty(dich)) return null;
        if (dich[0] == '/') return dich[1..];

        var phan = new List<string>((thuMuc + dich).Split('/', StringSplitOptions.RemoveEmptyEntries));

        for (var i = 0; i < phan.Count;)
        {
            if (phan[i] == ".") phan.RemoveAt(i);
            else if (phan[i] == ".." && i > 0) phan.RemoveRange(i - 1, 2);
            else i++;
        }

        return string.Join('/', phan);
    }

    private static string ThuMucCua(string duong)
    {
        var vach = duong.LastIndexOf('/');

        return vach < 0 ? string.Empty : duong[..(vach + 1)];
    }

    private static string TenTepCua(string duong) => duong[ThuMucCua(duong).Length..];

    private static XDocument? TaiXml(ZipArchive zip, string duong)
    {
        var muc = TimMuc(zip, duong);

        if (muc is null) return null;

        using var stream = muc.Open();
        using var doc = TaoBoDoc(stream);

        return XDocument.Load(doc);
    }

    /// <summary>
    /// Không giải DTD và không phân giải thực thể ngoài: file người khác đưa cho, không được để nó
    /// bảo công cụ đi đọc chỗ khác.
    /// </summary>
    private static XmlReader TaoBoDoc(Stream stream) =>
        XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = false,
        });

    private static ZipArchiveEntry? TimMuc(ZipArchive zip, string duong) =>
        zip.GetEntry(duong)
        ?? zip.Entries.FirstOrDefault(e => string.Equals(e.FullName, duong, StringComparison.OrdinalIgnoreCase));

    private static KetQuaDocFileExcel Hong(string loi) => new([], null, loi);

    /// <summary>File hỏng theo kiểu đã lường trước — nói ra được cho người kiểm, không phải sự cố.</summary>
    private sealed class LoiDocFile(string thongDiep) : Exception(thongDiep);
}
