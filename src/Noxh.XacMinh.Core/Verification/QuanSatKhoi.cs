using System.Globalization;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification;

/// <summary>
/// Hai chuỗi khối công khai được dùng làm mốc neo. Tên chuỗi trong báo cáo do người khác viết ra nên
/// đọc khoan dung (<c>eth</c>/<c>ethereum</c>), nhưng tên lạ thì trả <c>null</c> chứ không đoán: đoán
/// sai chuỗi là đi hỏi nhầm sổ cái rồi kết luận chắc nịch trên câu trả lời của sổ cái khác.
/// </summary>
public static class ChuoiKhoiNeo
{
    public const string Ethereum = "ethereum";

    public const string Bitcoin = "bitcoin";

    public static string? Chuan(string? ten) => ten?.Trim().ToLowerInvariant() switch
    {
        "ethereum" or "eth" => Ethereum,
        "bitcoin" or "btc" => Bitcoin,
        _ => null,
    };

    public static string TenHienThi(string chuoiKhoi) => chuoiKhoi == Bitcoin ? "Bitcoin" : "Ethereum";

    /// <summary>
    /// Link tra cứu thủ công trên nguồn công khai — thứ phải hiện ra khi công cụ không tự gọi được,
    /// để người kiểm đối chiếu bằng mắt thay vì tin công cụ nói "chưa kiểm được" rồi thôi.
    /// </summary>
    public static string Link(string chuoiKhoi, long doCao) => chuoiKhoi == Bitcoin
        ? $"https://blockstream.info/block-height/{doCao.ToString(CultureInfo.InvariantCulture)}"
        : $"https://etherscan.io/block/{doCao.ToString(CultureInfo.InvariantCulture)}";
}

/// <summary>Một block cần đọc từ nguồn công khai — lõi suy ra, vỏ UI đi gọi mạng theo đúng danh sách này.</summary>
public sealed record YeuCauTraCuuKhoi(string ChuoiKhoi, long DoCao)
{
    public string Link => ChuoiKhoiNeo.Link(ChuoiKhoi, DoCao);

    public string TenChuoi => ChuoiKhoiNeo.TenHienThi(ChuoiKhoi);
}

/// <summary>
/// Kết quả một lần tra cứu block trên nguồn công khai, đã trở thành <b>dữ liệu</b> trước khi vào lõi.
/// <see cref="Loi"/> khác <c>null</c> nghĩa là hỏi không được (mạng hỏng, bị chặn, nguồn trả rác) —
/// đó là một câu trả lời hợp lệ và phải dẫn tới CHƯA ĐỦ DỮ LIỆU, không phải một ngoại lệ bị nuốt.
/// </summary>
public sealed record QuanSatKhoi(
    string ChuoiKhoi,
    long DoCao,
    string? MaBam = null,
    DateTimeOffset? ThoiDiemDao = null,
    string? Nguon = null,
    string? Loi = null);

/// <summary>
/// Suy ra từ báo cáo xem phải đọc block nào: <b>chuỗi khối của từng vòng</b> ở <b>độ cao đã cam kết
/// của chính chuỗi đó</b>. Nằm trong lõi (thuần, tất định) để vỏ UI không phải tự diễn giải nghiệp vụ
/// — vỏ chỉ biết gọi mạng.
/// </summary>
public static class MocNeoTraCuu
{
    public static IReadOnlyList<YeuCauTraCuuKhoi> CanDoc(TransparencyReport report) =>
        (report.EntropySources ?? [])
            .Select(nguon => ChuoiKhoiNeo.Chuan(nguon?.AnchorChain))
            .Where(chuoi => chuoi is not null)
            .Select(chuoi => (ChuoiKhoi: chuoi!, DoCao: DoCaoDaCamKet(report, chuoi!)))
            .Where(x => x.DoCao is not null)
            .Select(x => new YeuCauTraCuuKhoi(x.ChuoiKhoi, x.DoCao!.Value))
            .Distinct()
            .ToList();

    /// <summary>Độ cao block đích đã chốt cho một chuỗi — con số nằm trong chuỗi được đóng dấu.</summary>
    public static long? DoCaoDaCamKet(TransparencyReport report, string chuoiKhoi) =>
        chuoiKhoi == ChuoiKhoiNeo.Bitcoin
            ? report.AnchorCommitment?.BtcTargetHeight
            : report.AnchorCommitment?.EthTargetHeight;
}
