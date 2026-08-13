namespace Noxh.XacMinh.Core.DanhSach;

/// <summary>
/// Kết quả đọc bảng dán vào. Còn <see cref="Loi"/> thì chưa kiểm được, không phải KHÔNG ĐẠT.
/// <see cref="CoDongTieuDe"/> để màn hình nói ra đã bỏ qua một dòng — người kiểm phải thấy con số
/// hồ sơ đọc được khớp biên bản, chứ không phải tin rằng công cụ bỏ đúng dòng cần bỏ.
/// </summary>
public sealed record KetQuaDocBang(IReadOnlyList<HoSoDanhSach> HoSo, IReadOnlyList<string> Loi, bool CoDongTieuDe);

/// <summary>
/// Đọc bảng danh sách người kiểm dán vào. Bốn cột theo đúng thứ tự biên bản in ra: mã hồ sơ · họ
/// tên · số định danh · nhóm đối tượng.
///
/// Ranh giới quan trọng nhất ở đây: cái gì thuộc <b>định dạng bảng</b> thì bỏ, cái gì thuộc <b>nội
/// dung ô</b> thì giữ nguyên văn.
///  · Ký tự xuống dòng kiểu Windows và dòng trống: định dạng — bỏ.
///  · Bảng kiểu <c>| a | b |</c> (dán từ tài liệu Markdown): thanh dọc và khoảng đệm quanh nó là kẻ
///    bảng — bỏ; dòng kẻ ngang <c>|---|---|</c> cũng vậy.
///  · Bảng ngăn bằng ký tự tab (dán từ bảng tính): nội dung ô lấy NGUYÊN VĂN, kể cả khoảng trắng
///    thừa. Tự cắt khoảng trắng ở đây là công cụ tự sửa dữ liệu cho khớp — mà verifier tự chỉnh dữ
///    liệu thì hết là verifier. Lệch vì khoảng trắng là việc của phần chẩn đoán, nó nói "khớp nếu…"
///    chứ không lặng lẽ sửa.
/// </summary>
public static class BangDanhSach
{
    private const int SoCot = 4;

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
