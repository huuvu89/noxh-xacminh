using System.Text;

namespace Noxh.XacMinh.Core.DanhSach;

/// <summary>
/// Bảng lệch mã băm thì phải nói được lệch <b>vì cái gì</b>: một danh sách còn nguyên vẫn lệch mã
/// băm nếu bản in mất khoảng trắng, đổi cách ghi dấu tiếng Việt, hay chèn dấu cách vào số định
/// danh. Chỗ này thử từng biến thể chuẩn hoá thường gặp rồi trả lời "khớp nếu …".
///
/// Hai điều nó KHÔNG làm, và không được để trôi thành có:
///  · Không sửa dữ liệu người kiểm dán vào. Biến thể chỉ dùng để chẩn đoán; kết luận vẫn là "bảng
///    bạn đang cầm khác bản đã khoá", vì verifier tự chỉnh dữ liệu cho khớp thì hết là verifier.
///  · Không đụng tới giá trị dữ liệu (chữ số của số định danh, chữ trong họ tên). Mọi biến thể ở
///    đây đều là cách <b>ghi</b> cùng một giá trị — nhờ vậy một số định danh bị đổi thật sẽ không
///    có biến thể nào khớp, và hạng mục ra KHÔNG ĐẠT chứ không ra một lời gợi ý xoa dịu.
/// </summary>
internal static class ChanDoanBang
{
    private sealed record BienThe(
        string MoTa, Func<HoSoDanhSach, HoSoDanhSach> Sua, bool TheoMaKyTu = false);

    private static readonly BienThe[] DanhSachBienThe =
    [
        new("bỏ khoảng trắng thừa ở đầu/cuối họ tên",
            h => h with { HoTen = h.HoTen.Trim() }),
        new("bỏ khoảng trắng thừa ở đầu/cuối mọi ô",
            h => new HoSoDanhSach(h.MaHoSo.Trim(), h.HoTen.Trim(), h.SoDinhDanh.Trim(), h.Nhom)),
        new("gộp các khoảng trắng lặp bên trong họ tên",
            h => h with { HoTen = GopKhoangTrang(h.HoTen) }),
        new("chuẩn hoá dấu tiếng Việt về dạng tổ hợp sẵn (NFC)",
            h => h with
            {
                MaHoSo = h.MaHoSo.Normalize(NormalizationForm.FormC),
                HoTen = h.HoTen.Normalize(NormalizationForm.FormC),
            }),
        new("chuẩn hoá dấu tiếng Việt về dạng tổ hợp rời (NFD)",
            h => h with
            {
                MaHoSo = h.MaHoSo.Normalize(NormalizationForm.FormD),
                HoTen = h.HoTen.Normalize(NormalizationForm.FormD),
            }),
        new("bỏ dấu cách và dấu chấm/gạch trong số định danh",
            h => h with { SoDinhDanh = BoDauNgan(h.SoDinhDanh) }),
        new("bỏ khoảng trắng thừa ở mọi ô và chuẩn hoá dấu tiếng Việt về dạng tổ hợp sẵn (NFC)",
            h => new HoSoDanhSach(
                h.MaHoSo.Trim().Normalize(NormalizationForm.FormC),
                h.HoTen.Trim().Normalize(NormalizationForm.FormC),
                h.SoDinhDanh.Trim(),
                h.Nhom)),
        new("sắp mã hồ sơ theo mã ký tự thay vì theo quy tắc so sánh của tiếng Việt",
            h => h, TheoMaKyTu: true),
    ];

    /// <summary>Mô tả biến thể đầu tiên cho ra đúng mã băm đã công bố; <c>null</c> nếu không có.</summary>
    public static string? BienTheKhop(
        IReadOnlyList<HoSoDanhSach> hoSo, byte[] khoa, string maBamDaCongBo) =>
        DanhSachBienThe
            .FirstOrDefault(b => string.Equals(
                MaBamDanhSach.Tinh(hoSo.Select(b.Sua).ToList(), khoa, b.TheoMaKyTu),
                maBamDaCongBo,
                StringComparison.Ordinal))
            ?.MoTa;

    private static string GopKhoangTrang(string giaTri) =>
        string.Join(' ', giaTri.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string BoDauNgan(string giaTri) =>
        new(giaTri.Where(c => c is not (' ' or '.' or '-' or '–')).ToArray());
}
