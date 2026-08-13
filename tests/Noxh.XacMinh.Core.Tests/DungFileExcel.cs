using ClosedXML.Excel;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Dựng file <c>.xlsx</c> <b>thật</b> cho test, bằng chính thư viện backend dùng để đọc file lúc
/// nhập danh sách. Bịa XML bằng tay ở đây thì test chỉ chứng minh bộ đọc tự viết đọc được thứ chính
/// nó tưởng tượng ra.
/// </summary>
internal static class DungFileExcel
{
    public const string TenTrangMacDinh = "Danh sách hồ sơ";

    public static byte[] Tu(IReadOnlyList<IReadOnlyList<object?>> dong, string tenTrang = TenTrangMacDinh) =>
        Tu([(tenTrang, dong)]);

    /// <summary>Nhiều trang tính để khẳng định công cụ đọc đúng trang ĐẦU TIÊN, như backend.</summary>
    public static byte[] Tu(IReadOnlyList<(string Ten, IReadOnlyList<IReadOnlyList<object?>> Dong)> trang)
    {
        using var wb = new XLWorkbook();

        foreach (var (ten, dong) in trang)
        {
            var ws = wb.AddWorksheet(ten);

            for (var r = 0; r < dong.Count; r++)
                for (var c = 0; c < dong[r].Count; c++)
                {
                    var giaTri = dong[r][c];

                    if (giaTri is null) continue;

                    var o = ws.Cell(r + 1, c + 1);

                    // Chuỗi phải ở lại là chuỗi: "079010000001" mà để Excel tự đoán kiểu thì có
                    // ngày nó thành số và mất số 0 đứng đầu — đúng cái bẫy của dữ liệu thật.
                    if (giaTri is string) o.Style.NumberFormat.Format = "@";

                    o.Value = XLCellValue.FromObject(giaTri);
                }
        }

        using var bo = new MemoryStream();
        wb.SaveAs(bo);

        return bo.ToArray();
    }

    /// <summary>Lưới ô đọc bằng ClosedXML — đúng cách <c>ImportExcelEndpoint</c> đọc từng ô.</summary>
    public static List<(int SoDong, List<string> O)> DocBangClosedXml(byte[] noiDung, int soCot)
    {
        using var wb = new XLWorkbook(new MemoryStream(noiDung));
        var ws = wb.Worksheets.First();

        return ws.RowsUsed()
            .Select(row => (
                row.RowNumber(),
                Enumerable.Range(1, soCot).Select(c => row.Cell(c).GetString()).ToList()))
            .ToList();
    }

    public static string TenTrangDauTien(byte[] noiDung)
    {
        using var wb = new XLWorkbook(new MemoryStream(noiDung));

        return wb.Worksheets.First().Name;
    }
}
