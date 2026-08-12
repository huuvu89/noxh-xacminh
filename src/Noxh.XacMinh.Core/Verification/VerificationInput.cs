using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification;

/// <summary>
/// Toàn bộ dữ liệu lõi kiểm được phép nhìn thấy. Lõi không gọi mạng: thứ gì cần mạng (block chuỗi
/// khối, trail bằng chứng) do vỏ UI lấy về rồi thêm vào đây dưới dạng dữ liệu.
/// </summary>
public sealed record VerificationInput(TransparencyReport Report);
