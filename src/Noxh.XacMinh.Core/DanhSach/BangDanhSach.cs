namespace Noxh.XacMinh.Core.DanhSach;

/// <summary>
/// Kết quả đọc bảng dán vào. Còn <see cref="Loi"/> thì chưa kiểm được, không phải KHÔNG ĐẠT.
/// <see cref="CoDongTieuDe"/> để màn hình nói ra đã bỏ qua một dòng — người kiểm phải thấy con số
/// hồ sơ đọc được khớp biên bản, chứ không phải tin rằng công cụ bỏ đúng dòng cần bỏ.
/// </summary>
public sealed record KetQuaDocBang(IReadOnlyList<HoSoDanhSach> HoSo, IReadOnlyList<string> Loi, bool CoDongTieuDe);

/// <summary>
/// Đọc bảng danh sách hồ sơ vào công cụ, bằng đường nào cũng ra cùng một thứ: danh sách
/// <see cref="HoSoDanhSach"/> cho phép tái lập mã băm.
///
/// Hai đường vào có <b>hai luật khác nhau về khoảng trắng</b>, và khác nhau là có chủ ý:
///  · <b>File Excel gốc</b> đi theo luật của hệ thống lúc nhập danh sách: nhận cột theo tên tiêu đề
///    rồi <c>Trim()</c> từng ô. Cắt khoảng trắng ở đây không phải tự sửa dữ liệu — đó chính là dữ
///    liệu đã đi vào mã băm, vì backend cắt trước khi ghi.
///  · <b>Bảng dán tay</b> lấy nội dung ô NGUYÊN VĂN. Người kiểm dán từ bản in/PDF thì không biết
///    chuỗi gốc là gì; công cụ tự cắt cho khớp thì hết là công cụ kiểm chứng. Lệch vì khoảng trắng
///    là việc của phần chẩn đoán, nó nói "khớp nếu…" chứ không lặng lẽ sửa.
/// </summary>
public static class BangDanhSach
{
    private const int SoCot = 4;

    public static KetQuaDocBang Doc(NguonBang nguon) => nguon switch
    {
        NguonBang.Dan dan => Doc(dan.NoiDung),
        NguonBang.Excel excel => DocExcel(excel.NoiDung),
        _ => new KetQuaDocBang([], [$"nguồn bảng không hiểu được ({nguon.GetType().Name})"], false),
    };

    /// <summary>
    /// Đọc bảng dán tay. Bốn cột theo đúng thứ tự biên bản in ra: mã hồ sơ · họ tên · số định danh ·
    /// nhóm đối tượng.
    ///
    /// Cái gì thuộc <b>định dạng bảng</b> thì bỏ, cái gì thuộc <b>nội dung ô</b> thì giữ nguyên văn:
    ///  · Ký tự xuống dòng kiểu Windows và dòng trống: định dạng — bỏ.
    ///  · Bảng kiểu <c>| a | b |</c> (dán từ tài liệu Markdown): thanh dọc và khoảng đệm quanh nó là
    ///    kẻ bảng — bỏ; dòng kẻ ngang <c>|---|---|</c> cũng vậy.
    ///  · Bảng ngăn bằng ký tự tab (dán thẳng từ bảng tính): nội dung ô lấy NGUYÊN VĂN.
    /// </summary>
    public static KetQuaDocBang Doc(string? bang)
    {
        var hoSo = new List<HoSoDanhSach>();
        var loi = new List<string>();
        var coTieuDe = false;

        if (string.IsNullOrWhiteSpace(bang)) return new KetQuaDocBang(hoSo, loi, coTieuDe);

        var dong = bang.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        for (var i = 0; i < dong.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(dong[i])) continue;

            var o = TachO(dong[i]);
            if (o is null) continue; // dòng kẻ ngang của bảng Markdown

            // Dòng ĐẦU không đọc ra được một hồ sơ là dòng tiêu đề — bỏ qua đúng một lần, và nói ra
            // là đã bỏ. Các dòng sau mà hỏng thì phải thành lỗi, kẻo một hồ sơ dán hỏng lặng lẽ
            // biến mất khỏi danh sách rồi công cụ đổ lỗi cho mã băm.
            var laHoSo = o.Length >= SoCot && NhomDoiTuong.Doc(o[SoCot - 1]) is not null;

            if (i == 0 && !laHoSo)
            {
                coTieuDe = true;
                continue;
            }

            if (o.Length != SoCot)
            {
                loi.Add($"dòng {i + 1}: đọc được {o.Length} cột, cần đúng {SoCot} cột "
                        + "(mã hồ sơ, họ tên, số định danh, nhóm đối tượng)");
                continue;
            }

            var nhom = NhomDoiTuong.Doc(o[3]);

            if (nhom is null)
            {
                loi.Add($"dòng {i + 1}: không đọc được nhóm đối tượng «{MoTaGiaTri.Gon(o[3])}»");
                continue;
            }

            hoSo.Add(new HoSoDanhSach(o[0], o[1], o[2], nhom.Value));
        }

        return new KetQuaDocBang(hoSo, loi, coTieuDe);
    }

    /// <summary>
    /// Đọc file Excel gốc theo đúng trình tự <c>ImportExcelEndpoint</c> đã đi lúc nhập danh sách:
    /// trang tính đầu tiên → dòng có dữ liệu đầu tiên là tiêu đề → nhận cột theo tên → mỗi ô
    /// <c>Trim()</c> → bỏ dòng trống hoàn toàn.
    /// </summary>
    private static KetQuaDocBang DocExcel(byte[] noiDung)
    {
        var file = FileExcel.Doc(noiDung);

        if (file.Loi is not null) return new KetQuaDocBang([], [file.Loi], false);

        if (file.Dong.Count == 0)
            return new KetQuaDocBang([], ["trang tính đầu tiên không có dòng nào có dữ liệu"], false);

        var tieuDe = file.Dong[0];
        var (cot, thieu) = CotDanhSach.DocTieuDe(tieuDe.O);

        if (thieu.Count > 0)
            return new KetQuaDocBang(
                [],
                [$"dòng tiêu đề (dòng {tieuDe.SoDong}) thiếu cột bắt buộc: "
                 + string.Join(", ", thieu.Select(t => $"«{t}»"))],
                CoDongTieuDe: true);

        var hoSo = new List<HoSoDanhSach>();
        var loi = new List<string>();

        foreach (var dong in file.Dong.Skip(1))
        {
            string O(CotDanhSach.Cot c) => cot.TryGetValue(c, out var i) ? dong.LayO(i).Trim() : string.Empty;

            var maHoSo = O(CotDanhSach.Cot.MaHoSo);
            var hoTen = O(CotDanhSach.Cot.HoTen);
            var soDinhDanh = O(CotDanhSach.Cot.SoDinhDanh);

            // Đúng luật bỏ dòng trống của backend — kể cả cột số điện thoại, thứ mã băm không dùng
            // nhưng lại quyết định một dòng có bị bỏ hay không.
            if (maHoSo.Length == 0 && hoTen.Length == 0 && soDinhDanh.Length == 0
                && O(CotDanhSach.Cot.SoDienThoai).Length == 0)
                continue;

            var oNhom = O(CotDanhSach.Cot.Nhom);
            var nhom = NhomDoiTuong.Doc(oNhom);

            if (nhom is null)
            {
                loi.Add($"dòng {dong.SoDong}: không đọc được nhóm đối tượng «{MoTaGiaTri.Gon(oNhom)}»");
                continue;
            }

            hoSo.Add(new HoSoDanhSach(maHoSo, hoTen, soDinhDanh, nhom.Value));
        }

        return new KetQuaDocBang(hoSo, loi, CoDongTieuDe: true);
    }

    /// <summary>Tách một dòng thành các ô; <c>null</c> nếu dòng đó chỉ là kẻ bảng.</summary>
    private static string[]? TachO(string dong)
    {
        if (!dong.TrimStart().StartsWith('|')) return BoOTrongOCuoi(dong.Split('\t'));

        var trong = dong.Trim().Trim('|');

        if (trong.All(c => c is '-' or ':' or '|' or ' ')) return null;

        return BoOTrongOCuoi(trong.Split('|').Select(o => o.Trim()).ToArray());
    }

    /// <summary>Ô rỗng ở cuối dòng là vết của dấu ngăn thừa, không phải một cột dữ liệu.</summary>
    private static string[] BoOTrongOCuoi(string[] o)
    {
        var het = o.Length;
        while (het > 0 && o[het - 1].Length == 0) het--;

        return het == o.Length ? o : o[..het];
    }
}
