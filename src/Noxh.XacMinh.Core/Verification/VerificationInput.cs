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
/// </summary>
public sealed record VerificationInput(
    TransparencyReport Report,
    UnitCatalog? Catalog = null,
    IReadOnlyList<QuanSatKhoi>? Blocks = null);
