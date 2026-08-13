using System.IO.Compression;
using System.Text;
using Noxh.XacMinh.Core.DanhSach;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #17 — bộ đọc <c>.xlsx</c> tự viết. Nó tồn tại để bản WebAssembly khỏi phải cõng ClosedXML +
/// OpenXml + Irony, và cái giá của việc tự viết được trả ở đây: <b>so từng ô với chính ClosedXML
/// 0.102.3</b>, đúng phiên bản backend dùng lúc nhập danh sách. Lệch cách đọc là test đỏ, chứ không
/// phải một kết luận sai giữa hội trường.
/// </summary>
public class FileExcelTests
{
    private const int SoCotSo = 6;

    /// <summary>
    /// Đủ các kiểu ô một file thật có thể mang: chuỗi (đi qua bảng chuỗi chung), chuỗi thừa khoảng
    /// trắng, chuỗi trông như số, số nguyên, số lẻ, luận lý, công thức, ô trống, ô chỉ có khoảng
    /// trắng.
    /// </summary>
    private static byte[] FileDuKieuO() => DungFileExcel.Tu(
    [
        ["Mã hồ sơ", "Họ và tên", "CCCD", "Nhóm đối tượng", "Số", "Cờ"],
        ["HS001", "  Trần Thị Bình  ", "079010000001", "U1", 1.5, true],
        ["HS002", "Lê Hữu Cường", "079010000002", 1.1, 1234567890123L, false],
        [null, "   ", null, "U6", null, null],
        ["HS004", "Phạm Đình Đ", "079010000004", "U6 – Đối tượng thường", 0, null],
    ]);

    [Fact]
    public void DocTungO_GiongHetClosedXml_TrenMoiKieuO()
    {
        var file = FileDuKieuO();

        var cuaCongCu = FileExcel.Doc(file);
        var cuaThuVien = DungFileExcel.DocBangClosedXml(file, SoCotSo);

        Assert.Null(cuaCongCu.Loi);
        Assert.Equal(cuaThuVien.Select(d => d.SoDong), cuaCongCu.Dong.Select(d => d.SoDong));

        foreach (var (mong, thuc) in cuaThuVien.Zip(cuaCongCu.Dong))
            Assert.Equal(mong.O, Enumerable.Range(0, SoCotSo).Select(thuc.LayO));
    }

    /// <summary>Dòng trống hoàn toàn không phải một dòng — nhưng số dòng của các dòng sau phải giữ
    /// nguyên, nếu không thông báo lỗi sẽ chỉ sai chỗ.</summary>
    [Fact]
    public void DongTrongHoanToan_BiBoQua_NhungSoDongVanLaSoDongThat()
    {
        var file = DungFileExcel.Tu(
        [
            ["Mã hồ sơ", "Họ và tên"],
            [null, null],
            [null, null],
            ["HS009", "Vũ Hữu Mai"],
        ]);

        var ketQua = FileExcel.Doc(file);

        Assert.Equal([1, 4], ketQua.Dong.Select(d => d.SoDong));
        Assert.Equal("HS009", ketQua.Dong[1].LayO(0));
    }

    /// <summary>Ô chỉ có khoảng trắng VẪN là ô có nội dung — đúng như ClosedXML, và đúng cả về đạo
    /// lý: công cụ kiểm chứng không được tự dọn dữ liệu.</summary>
    [Fact]
    public void ODuyNhatChiCoKhoangTrang_VanLaDongCoDuLieu()
    {
        var file = DungFileExcel.Tu([["Mã hồ sơ"], ["   "]]);

        var cuaCongCu = FileExcel.Doc(file);

        Assert.Equal(DungFileExcel.DocBangClosedXml(file, 1).Select(d => d.SoDong),
            cuaCongCu.Dong.Select(d => d.SoDong));
        Assert.Equal("   ", cuaCongCu.Dong[1].LayO(0));
    }

    [Fact]
    public void FileNhieuTrang_DocTrangDauTien_DungTrangClosedXmlChon()
    {
        var file = DungFileExcel.Tu(
        [
            ("Danh sách", [["Mã hồ sơ"], ["HS001"]]),
            ("Ghi chú", [["Mã hồ sơ"], ["HS999"]]),
        ]);

        var ketQua = FileExcel.Doc(file);

        Assert.Equal(DungFileExcel.TenTrangDauTien(file), ketQua.TenTrang);
        Assert.Equal("HS001", ketQua.Dong[1].LayO(0));
    }

    /// <summary>Chuỗi bị Excel cắt thành nhiều đoạn định dạng phải nối lại đúng thứ tự.</summary>
    [Fact]
    public void ChuoiChungNhieuDoanDinhDang_NoiLaiDungNguyenVan()
    {
        var noiDung = XlsxThuCong(
            """<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><si><r><t xml:space="preserve">Trần </t></r><r><t>Thị</t></r><r><t xml:space="preserve"> Bình</t></r></si></sst>""",
            """<row r="1"><c r="A1" t="s"><v>0</v></c></row>""");

        Assert.Equal("Trần Thị Bình", FileExcel.Doc(noiDung).Dong[0].LayO(0));
    }

    /// <summary>Vài bộ xuất Excel ghi chuỗi thẳng vào ô (<c>inlineStr</c>) thay vì qua bảng chung.</summary>
    [Fact]
    public void ChuoiGhiThangTrongO_inlineStr_DocDuoc()
    {
        var noiDung = XlsxThuCong(
            null,
            """<row r="1"><c r="A1" t="inlineStr"><is><t xml:space="preserve"> Lê Hữu Cường </t></is></c></row>""");

        Assert.Equal(" Lê Hữu Cường ", FileExcel.Doc(noiDung).Dong[0].LayO(0));
    }

    /// <summary>Ô thưa: cột B trống thì cột C vẫn phải nằm ở chỉ số 2, không dồn lên.</summary>
    [Fact]
    public void OThuaKhongLienTiep_GiuDungChiSoCot()
    {
        var noiDung = XlsxThuCong(
            null,
            """<row r="1"><c r="A1" t="str"><v>a</v></c><c r="C1" t="str"><v>c</v></c><c r="AB1" t="str"><v>ab</v></c></row>""");

        var dong = FileExcel.Doc(noiDung).Dong[0];

        Assert.Equal("a", dong.LayO(0));
        Assert.Equal(string.Empty, dong.LayO(1));
        Assert.Equal("c", dong.LayO(2));
        Assert.Equal("ab", dong.LayO(27));
    }

    // ── File không đọc được: nói ra được, không ném ngoại lệ lên màn hình ─────────────────

    [Fact]
    public void FileXlsDoiCu_NoiRoPhaiLuuThanhXlsx()
    {
        byte[] xlsCu = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0];

        var loi = FileExcel.Doc(xlsCu).Loi;

        Assert.NotNull(loi);
        Assert.Contains(".xls", loi, StringComparison.Ordinal);
        Assert.Contains(".xlsx", loi, StringComparison.Ordinal);
    }

    [Fact]
    public void FileKhongPhaiGoiNen_ChoLoiDocDuoc_KhongNemNgoaiLe()
    {
        var loi = FileExcel.Doc(Encoding.UTF8.GetBytes("Mã hồ sơ\tHọ tên\n")).Loi;

        Assert.NotNull(loi);
        Assert.Contains(".xlsx", loi, StringComparison.Ordinal);
    }

    [Fact]
    public void FileRong_ChoLoiDocDuoc()
    {
        Assert.NotNull(FileExcel.Doc([]).Loi);
    }

    [Fact]
    public void GoiNenNhungKhongPhaiSoBangTinh_ChoLoiDocDuoc()
    {
        using var bo = new MemoryStream();

        using (var zip = new ZipArchive(bo, ZipArchiveMode.Create, leaveOpen: true))
        using (var ghi = new StreamWriter(zip.CreateEntry("doc.txt").Open()))
            ghi.Write("không phải bảng tính");

        Assert.NotNull(FileExcel.Doc(bo.ToArray()).Loi);
    }

    [Fact]
    public void XmlBenTrongHong_ChoLoiDocDuoc_KhongNemNgoaiLe()
    {
        var noiDung = XlsxThuCong(null, "<row r=\"1\"><c r=\"A1\"");

        Assert.NotNull(FileExcel.Doc(noiDung).Loi);
    }

    /// <summary>
    /// File người khác đưa cho không được bảo công cụ đi đọc chỗ khác: khai báo DTD phải bị từ chối
    /// thẳng, không phải được xử lý rồi mới hy vọng không sao.
    /// </summary>
    [Fact]
    public void KhaiBaoDtdTrongFile_BiTuChoi()
    {
        var noiDung = XlsxThuCong(
            null,
            "<row r=\"1\"><c r=\"A1\" t=\"str\"><v>a</v></c></row>",
            dtd: "<!DOCTYPE worksheet [<!ENTITY x \"y\">]>");

        Assert.NotNull(FileExcel.Doc(noiDung).Loi);
    }

    /// <summary>
    /// Gói <c>.xlsx</c> tối thiểu viết tay — cần cho những ca ClosedXML không bao giờ tự sinh ra
    /// (<c>inlineStr</c>, ô thưa, XML hỏng, DTD).
    /// </summary>
    private static byte[] XlsxThuCong(string? bangChuoiChung, string dongSheet, string? dtd = null)
    {
        const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string nsQh = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        using var bo = new MemoryStream();

        using (var zip = new ZipArchive(bo, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Ghi(string ten, string noiDung)
            {
                using var ghi = new StreamWriter(zip.CreateEntry(ten).Open(), new UTF8Encoding(false));
                ghi.Write(noiDung);
            }

            Ghi("_rels/.rels",
                $"""<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="{nsQh}/officeDocument" Target="xl/workbook.xml"/></Relationships>""");

            Ghi("xl/workbook.xml",
                $"""<?xml version="1.0" encoding="UTF-8"?><workbook xmlns="{ns}" xmlns:r="{nsQh}"><sheets><sheet name="Danh sách" sheetId="1" r:id="rId1"/></sheets></workbook>""");

            Ghi("xl/_rels/workbook.xml.rels",
                $"""<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="{nsQh}/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="{nsQh}/sharedStrings" Target="sharedStrings.xml"/></Relationships>""");

            if (bangChuoiChung is not null)
                Ghi("xl/sharedStrings.xml", $"""<?xml version="1.0" encoding="UTF-8"?>{bangChuoiChung}""");

            Ghi("xl/worksheets/sheet1.xml",
                $"""<?xml version="1.0" encoding="UTF-8"?>{dtd}<worksheet xmlns="{ns}"><sheetData>{dongSheet}</sheetData></worksheet>""");
        }

        return bo.ToArray();
    }
}
