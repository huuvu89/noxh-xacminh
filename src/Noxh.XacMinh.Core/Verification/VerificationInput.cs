using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Units;

namespace Noxh.XacMinh.Core.Verification;

/// <summary>
/// Toàn bộ dữ liệu lõi kiểm được phép nhìn thấy. Lõi không gọi mạng: thứ gì cần mạng (block chuỗi
/// khối, trail bằng chứng) do vỏ UI lấy về rồi thêm vào đây dưới dạng dữ liệu.
/// <see cref="Catalog"/> là danh mục căn đang dùng — dữ liệu đầu vào do ban tổ chức công bố, không
/// phải thứ công cụ tự chứng minh; thiếu nó thì vòng phân căn ưu tiên ra KHÔNG KIỂM ĐƯỢC.
/// </summary>
public sealed record VerificationInput(TransparencyReport Report, UnitCatalog? Catalog = null);
