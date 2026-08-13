using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Noxh.XacMinh.Core.Crypto;

namespace Noxh.XacMinh.Core.DanhSach;

/// <summary>
/// Mã băm danh sách hồ sơ đầu vào (<c>ListHash</c>) — COPY NGUYÊN VĂN từ backend
/// (<c>LockListEndpoint</c> + <c>CryptoHelper.Sha256OfObject</c> + <c>HmacBlindIndex</c>):
///
///  1. Sắp danh sách theo mã hồ sơ.
///  2. Mỗi hồ sơ thành một đối tượng <c>{MaHoSo, CccdBlindIndex, FullName, Group}</c> — số định danh
///     KHÔNG đi vào chuỗi băm, chỉ có <b>chỉ mục mù</b> HMAC-SHA256 của nó.
///  3. Băm SHA-256 trên chuỗi do <b>bộ tuần tự hoá JSON mặc định của .NET</b> sinh ra.
///
/// Bước 3 là chỗ bẫy: bộ tuần tự mặc định giữ tên trường kiểu .NET (<c>MaHoSo</c>, không phải
/// <c>maHoSo</c>), ghi nhóm đối tượng ra <b>số</b> chứ không phải chữ, và escape mọi ký tự ngoài
/// ASCII thành <c>\uXXXX</c> — nên tên tiếng Việt trong chuỗi băm không còn là tiếng Việt. Port
/// sang ngôn ngữ khác gần như chắc chắn sai một trong ba chỗ đó, và cái sai ấy đổ hết lên đầu danh
/// sách hồ sơ.
/// </summary>
public static class MaBamDanhSach
{
    /// <summary>COPY NGUYÊN VĂN từ backend: HMAC-SHA256 trên số định danh viết HOA, hex thường.</summary>
    public static string ChiMucMu(byte[] khoa, string soDinhDanh)
    {
        using var hmac = new HMACSHA256(khoa);

        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(soDinhDanh.ToUpperInvariant())))
            .ToLowerInvariant();
    }

    /// <summary>Chuỗi JSON đem băm — phơi ra để có test vector ghim, KHÔNG để đưa lên màn hình.</summary>
    public static string ChuoiDemBam(IReadOnlyList<HoSoDanhSach> hoSo, byte[] khoa, bool theoMaKyTu = false) =>
        JsonSerializer.Serialize(
            // Backend gọi OrderBy trần trên chuỗi ⇒ so sánh theo ngôn ngữ, không phải theo mã ký
            // tự. Dùng ngôn ngữ bất biến chứ không phải ngôn ngữ của trình duyệt: cùng một bảng
            // phải ra cùng một mã băm dù mở trên máy nào.
            hoSo.OrderBy(h => h.MaHoSo, theoMaKyTu ? StringComparer.Ordinal : StringComparer.InvariantCulture)
                .Select(h => new DongDanhSach(h.MaHoSo, ChiMucMu(khoa, h.SoDinhDanh), h.HoTen, h.Nhom))
                .ToList(),
            DanhSachJsonContext.Default.ListDongDanhSach);

    /// <summary>
    /// Mã băm danh sách, hex thường — chính là <c>ListHash</c> đã ghim.
    /// <paramref name="theoMaKyTu"/> sắp mã hồ sơ theo mã ký tự thay vì theo quy tắc so sánh của
    /// ngôn ngữ; hai cách chỉ khác nhau khi mã hồ sơ có dấu gạch, khoảng trắng hay chữ có dấu —
    /// hiếm, nhưng khi xảy ra thì mã băm lệch mà không có ô dữ liệu nào sai, nên đó là một trong
    /// các biến thể phải thử khi chẩn đoán.
    /// </summary>
    public static string Tinh(IReadOnlyList<HoSoDanhSach> hoSo, byte[] khoa, bool theoMaKyTu = false) =>
        Hex.Sha256Hex(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            .GetBytes(ChuoiDemBam(hoSo, khoa, theoMaKyTu)));
}

/// <summary>Đúng hình dạng đối tượng backend đem tuần tự hoá — thứ tự trường cũng là một phần của nó.</summary>
internal sealed record DongDanhSach(string MaHoSo, string CccdBlindIndex, string FullName, int Group);

/// <summary>
/// Sinh mã tuần tự hoá lúc biên dịch: bản publish của Blazor WASM có trimming, mà trimming cắt mất
/// thuộc tính chỉ được reflection chạm tới thì chuỗi đem băm sẽ thiếu trường — và công cụ sẽ báo
/// KHÔNG ĐẠT cho một danh sách còn nguyên, chỉ khi chạy trên trang thật.
/// </summary>
[JsonSerializable(typeof(List<DongDanhSach>))]
internal partial class DanhSachJsonContext : JsonSerializerContext;
