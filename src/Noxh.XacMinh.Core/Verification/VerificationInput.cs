using Noxh.XacMinh.Core.DanhSach;
using Noxh.XacMinh.Core.Kho;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Units;

namespace Noxh.XacMinh.Core.Verification;

/// <summary>
/// Toàn bộ dữ liệu lõi kiểm được phép nhìn thấy. Lõi không gọi mạng: thứ gì cần mạng (block chuỗi
/// khối, trail bằng chứng) do vỏ UI lấy về rồi thêm vào đây dưới dạng dữ liệu.
/// <see cref="Catalog"/> là danh mục căn đang dùng — dữ liệu đầu vào do ban tổ chức công bố, không
/// phải thứ công cụ tự chứng minh; thiếu nó thì vòng phân căn ưu tiên ra KHÔNG KIỂM ĐƯỢC.
/// <see cref="Blocks"/> là block đọc được từ nguồn công khai (vỏ UI đi hỏi theo
/// <see cref="MocNeoTraCuu.CanDoc"/>); trống thì hạng mục mốc neo ra KHÔNG KIỂM ĐƯỢC kèm link tra
/// cứu thủ công, không bao giờ ra ĐẠT.
/// <see cref="DanhSach"/> là bảng danh sách hồ sơ và khoá chỉ mục mù tổ giám sát dán vào — dữ liệu
/// nhạy cảm nhất đi qua công cụ, chỉ sống trong bộ nhớ tab và không rời khỏi lõi này.
/// <see cref="Kho"/> là các lô bằng chứng vỏ UI đọc được từ kho lưu trữ chỉ-ghi (trong lễ: bằng khoá
/// chỉ-đọc; sau lễ: ẩn danh); trống thì hai hạng mục trail ra KHÔNG KIỂM ĐƯỢC và <b>không</b> làm
/// đổi kết luận của hạng mục nào khác.
/// </summary>
public sealed record VerificationInput(
    TransparencyReport Report,
    UnitCatalog? Catalog = null,
    IReadOnlyList<QuanSatKhoi>? Blocks = null,
    DanhSachDauVao? DanhSach = null,
    KhoBangChung? Kho = null);
