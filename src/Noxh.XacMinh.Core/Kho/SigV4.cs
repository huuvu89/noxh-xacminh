using System.Security.Cryptography;
using System.Text;

namespace Noxh.XacMinh.Core.Kho;

public sealed record TieuDeYeuCau(string Ten, string GiaTri);

public sealed record ThamSoTruyVan(string Ten, string GiaTri);

/// <summary>Một request đã đủ dữ kiện để ký. Không có byte thân request: công cụ chỉ đọc.</summary>
public sealed record YeuCauKy(
    string PhuongThuc,
    string DuongDan,
    IReadOnlyList<ThamSoTruyVan> ThamSo,
    IReadOnlyList<TieuDeYeuCau> TieuDe,
    string MaBamThan,
    DateTimeOffset ThoiDiem,
    string Vung,
    string DichVu);

/// <summary>
/// Ký request theo chuẩn AWS Signature Version 4 — <b>hàm thuần</b>: cùng request, cùng khoá, cùng
/// thời điểm thì ra cùng một chuỗi, không đụng mạng, không đụng đồng hồ.
///
/// Vì sao tự viết thay vì kéo AWS SDK vào: công cụ này là một trang WebAssembly người kiểm tải về,
/// mà SDK kéo theo vài MB và dựa nhiều vào reflection nên bản publish có trimming rất dễ hỏng đúng
/// lúc chạy thật. Đổi lại phải trả giá bằng test, và giá đó trả bằng <b>bộ vector chuẩn AWS công
/// bố</b> (<c>SigV4Tests</c>) chứ không phải bằng kết quả của chính nó.
///
/// <b>Chỉ ký được GET/HEAD.</b> Đây là chỗ cưỡng chế lời hứa "chỉ nhận khoá chỉ-đọc": người kiểm có
/// dán nhầm khoá có quyền ghi thì công cụ vẫn không dựng nổi một request sửa/xoá kho bằng chứng —
/// và kho vốn cũng không cho xoá.
/// </summary>
public static class SigV4
{
    /// <summary>SHA-256 của thân rỗng — mọi request đọc đều dùng giá trị này.</summary>
    public const string MaBamThanRong = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private const string ThuatToan = "AWS4-HMAC-SHA256";

    private const string KyTuAnToan = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

    public static string Ky(YeuCauKy yeu, KhoaKho khoa)
    {
        var phuongThuc = yeu.PhuongThuc.ToUpperInvariant();

        if (phuongThuc is not ("GET" or "HEAD"))
            throw new NotSupportedException(
                $"Công cụ kiểm chỉ đọc kho bằng chứng nên chỉ ký GET/HEAD, không ký «{yeu.PhuongThuc}».");

        var ngay = yeu.ThoiDiem.UtcDateTime.ToString("yyyyMMdd");
        var ngayGio = yeu.ThoiDiem.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");
        var phamVi = $"{ngay}/{yeu.Vung}/{yeu.DichVu}/aws4_request";

        var tieuDe = yeu.TieuDe
            .Select(t => (Ten: t.Ten.ToLowerInvariant(), GiaTri: GomKhoangTrang(t.GiaTri)))
            .OrderBy(t => t.Ten, StringComparer.Ordinal)
            .ToList();
        var danhSachTieuDe = string.Join(';', tieuDe.Select(t => t.Ten));

        var chuanHoa = string.Join('\n',
            phuongThuc,
            MaHoaDuongDan(yeu.DuongDan),
            ChuoiTruyVan(yeu.ThamSo),
            string.Concat(tieuDe.Select(t => $"{t.Ten}:{t.GiaTri}\n")),
            danhSachTieuDe,
            yeu.MaBamThan);

        var chuoiDemKy = string.Join('\n', ThuatToan, ngayGio, phamVi, Hex(SHA256.HashData(Utf8(chuanHoa))));
        var chuKy = Hex(HMACSHA256.HashData(KhoaKy(khoa.BiMat, ngay, yeu.Vung, yeu.DichVu), Utf8(chuoiDemKy)));

        return $"{ThuatToan} Credential={khoa.MaKhoa}/{phamVi}, SignedHeaders={danhSachTieuDe}, Signature={chuKy}";
    }

    /// <summary>Khoá ký dẫn xuất theo ngày/vùng/dịch vụ — tách ra vì AWS công bố vector riêng cho nó.</summary>
    public static string KhoaKyHex(string biMat, string ngay, string vung, string dichVu) =>
        Hex(KhoaKy(biMat, ngay, vung, dichVu));

    /// <summary>Mã hoá phần trăm theo RFC 3986 — khoảng trắng ra <c>%20</c>, không phải <c>+</c>.</summary>
    public static string MaHoa(string giaTri)
    {
        var sb = new StringBuilder(giaTri.Length);

        foreach (var byteGiaTri in Utf8(giaTri))
        {
            var ky = (char)byteGiaTri;
            if (byteGiaTri < 128 && KyTuAnToan.IndexOf(ky) >= 0) sb.Append(ky);
            else sb.Append('%').Append(byteGiaTri.ToString("X2"));
        }

        return sb.ToString();
    }

    /// <summary>Tham số sắp theo thứ tự byte của tên rồi tới giá trị — đúng thứ tự chuẩn hoá của AWS.</summary>
    public static string ChuoiTruyVan(IReadOnlyList<ThamSoTruyVan> thamSo) =>
        string.Join('&', thamSo
            .Select(t => (Ten: MaHoa(t.Ten), GiaTri: MaHoa(t.GiaTri)))
            .OrderBy(t => t.Ten, StringComparer.Ordinal)
            .ThenBy(t => t.GiaTri, StringComparer.Ordinal)
            .Select(t => $"{t.Ten}={t.GiaTri}"));

    private static byte[] KhoaKy(string biMat, string ngay, string vung, string dichVu)
    {
        var khoa = HMACSHA256.HashData(Utf8("AWS4" + biMat), Utf8(ngay));
        khoa = HMACSHA256.HashData(khoa, Utf8(vung));
        khoa = HMACSHA256.HashData(khoa, Utf8(dichVu));

        return HMACSHA256.HashData(khoa, Utf8("aws4_request"));
    }

    /// <summary>Đường dẫn mã hoá từng đoạn, giữ nguyên dấu <c>/</c> ngăn đoạn.</summary>
    private static string MaHoaDuongDan(string duongDan)
    {
        if (string.IsNullOrEmpty(duongDan)) return "/";

        return string.Join('/', duongDan.Split('/').Select(MaHoa));
    }

    /// <summary>Giá trị tiêu đề: cắt hai đầu và gom các khoảng trắng liền nhau thành một.</summary>
    private static string GomKhoangTrang(string giaTri)
    {
        var sb = new StringBuilder(giaTri.Length);
        var vuaCoKhoangTrang = false;

        foreach (var ky in giaTri.Trim())
        {
            if (ky == ' ')
            {
                if (!vuaCoKhoangTrang) sb.Append(ky);
                vuaCoKhoangTrang = true;
            }
            else
            {
                sb.Append(ky);
                vuaCoKhoangTrang = false;
            }
        }

        return sb.ToString();
    }

    private static byte[] Utf8(string giaTri) => Encoding.UTF8.GetBytes(giaTri);

    private static string Hex(byte[] byteGiaTri) => Convert.ToHexString(byteGiaTri).ToLowerInvariant();
}
