using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Noxh.XacMinh.Core.Kho;

/// <summary>
/// Bóc <b>gói trail</b> — kho bằng chứng đã được tải sẵn bằng script rồi nén thành một file
/// <c>.zip</c> — ra đúng thứ mà đường đọc thẳng kho đưa vào lõi. Có đường này vì hai hoàn cảnh có
/// thật: kho chưa bật CORS thì trình duyệt không đọc nổi kho dù kho đã mở, và bản offline thì máy
/// người kiểm vốn không có mạng.
///
/// Nguyên tắc dựng khuôn gói, và cũng là lý do gói trông "thừa" như thế này:
///  · Gói <b>chỉ chở byte thô</b>. Mã băm từng lô do công cụ tự tính lại trên đúng byte trong gói
///    (<see cref="DocLoBangChung"/>) — script mà khai hộ mã băm thì cả chuỗi móc xích mất sạch giá
///    trị chứng minh, vì lúc đó công cụ chỉ còn kiểm lời khai của chính script.
///  · Gói chở <b>nguyên văn XML <c>ListObjectsV2</c></b> chứ không chở danh sách key đã bóc sẵn:
///    danh sách và "đã đọc hết kho hay chưa" được suy ra ở đây bằng chính
///    <see cref="LietKeKho"/> đang được test, nên bề mặt phải tin script thu về đúng một mệnh đề —
///    <i>đống byte này lấy từ kho ấy</i>.
///  · <c>manifest.json</c> chỉ là <b>lời khai để hiển thị</b> (địa chỉ kho, thời điểm tải). Không
///    một kết luận nào được phép đổi theo nó, nên thiếu manifest cũng vẫn bóc được gói.
/// </summary>
public static class DocGoiTrail
{
    public const string PhienBanChuan = "NOXH-TRAIL-GOI-v1";

    public const string TenManifest = "manifest.json";

    public const string ThuMucTrangDanhSach = "listing/";

    public const string ThuMucNoiDung = "objects/";

    /// <summary>Trần số lô bóc một lần — bằng trần của đường đọc thẳng, để hai đường nói cùng một thứ.</summary>
    private const int GioiHanLo = 5000;

    /// <summary>Trần byte một mục và cả gói: gói là file lạ, đừng để nó nuốt hết RAM của tab.</summary>
    private const long GioiHanByteMotMuc = 64L * 1024 * 1024;

    private const long GioiHanByteCaGoi = 512L * 1024 * 1024;

    public static KhoBangChung Doc(byte[] goi, string tenGoi)
    {
        KhoBangChung Hong(string loi, string? nguon = null) =>
            KhoBangChung.Hong(CheDoDocKho.GoiNhapTay, loi, nguon ?? MoTaNguonToiThieu(tenGoi));

        ZipArchive kho;

        try
        {
            kho = new ZipArchive(new MemoryStream(goi, writable: false), ZipArchiveMode.Read);
        }
        catch (InvalidDataException ex)
        {
            return Hong($"không mở được gói (file không phải .zip đọc được): {ex.Message}");
        }

        using (kho)
        {
            if (kho.Entries.Sum(m => Math.Max(m.Length, 0)) > GioiHanByteCaGoi)
                return Hong("gói khai kích thước sau khi giải nén lớn bất thường — dừng lại thay vì "
                            + "treo tab người kiểm");

            var khaiBao = DocManifest(kho);
            var nguon = MoTaNguon(tenGoi, khaiBao);

            var trang = kho.Entries
                .Where(m => m.FullName.StartsWith(ThuMucTrangDanhSach, StringComparison.Ordinal) && m.Length > 0)
                .OrderBy(m => m.FullName, StringComparer.Ordinal)
                .ToList();

            if (trang.Count == 0)
                return Hong(
                    $"gói không có trang danh sách nào ({ThuMucTrangDanhSach}…): không biết kho khai có "
                    + "những lô nào thì không kiểm được lô nào thiếu — hãy tải lại gói bằng script.",
                    nguon);

            var key = new List<string>();
            var daLietKeHet = true;

            for (var i = 0; i < trang.Count; i++)
            {
                var (than, loiDoc) = DocVanBan(trang[i]);

                if (loiDoc is not null)
                    return Hong($"trang danh sách «{trang[i].FullName}» đọc không được: {loiDoc}", nguon);

                var daBoc = LietKeKho.Doc(than!);

                // Kho từ chối cũng trả về XML lỗi: giữ nguyên mã đó, vì "AccessDenied lúc tải" khác
                // hẳn "kho rỗng", và người kiểm cần biết gói họ cầm được tải bằng khoá không đủ quyền.
                if (daBoc.Loi is not null)
                    return Hong($"trang danh sách «{trang[i].FullName}»: {daBoc.Loi}", nguon);

                key.AddRange(daBoc.Key);

                // Trang giữa mà đã hết dấu tiếp tục nghĩa là gói ghép từ nhiều lần liệt kê rời rạc:
                // vẫn bóc, nhưng không được khai là đã đọc hết kho.
                var conTrangSau = daBoc.DauTiepTuc is not null;
                if (conTrangSau == (i == trang.Count - 1)) daLietKeHet = false;
            }

            if (key.Count > GioiHanLo)
            {
                key = key.Take(GioiHanLo).ToList();
                daLietKeHet = false;
            }

            var theoKey = kho.Entries
                .Where(m => m.FullName.StartsWith(ThuMucNoiDung, StringComparison.Ordinal)
                            && !m.FullName.EndsWith('/'))
                .GroupBy(m => m.FullName[ThuMucNoiDung.Length..], StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var doiTuong = new List<DoiTuongKho>(key.Count);

            foreach (var motKey in key.Distinct(StringComparer.Ordinal))
            {
                if (!theoKey.TryGetValue(motKey, out var muc))
                {
                    // Có trong danh sách kho mà gói không chở nội dung: đúng nghĩa "tải không được",
                    // không phải "kho giấu lô" — lõi phân biệt hai chuyện đó.
                    doiTuong.Add(new DoiTuongKho(motKey, [],
                        $"gói không chứa nội dung lô này ({ThuMucNoiDung}{motKey})"));
                    continue;
                }

                var (byteLo, loiLo) = DocByte(muc);
                doiTuong.Add(loiLo is null
                    ? new DoiTuongKho(motKey, byteLo!)
                    : new DoiTuongKho(motKey, [], $"đọc nội dung trong gói không được: {loiLo}"));
            }

            var thua = theoKey.Keys.Where(k => !key.Contains(k, StringComparer.Ordinal)).ToList();

            return KhoBangChung.Doc(CheDoDocKho.GoiNhapTay, doiTuong, nguon + MoTaThua(thua), daLietKeHet);
        }
    }

    // ── Lời khai của gói: chỉ để hiển thị, không đổi được kết luận nào ───────────────────────

    private sealed record KhaiBaoGoi(string? PhienBan, string? DiemCuoi, string? Bucket, string? TienTo, string? TaoLuc);

    private static KhaiBaoGoi? DocManifest(ZipArchive kho)
    {
        var muc = kho.GetEntry(TenManifest);

        if (muc is null) return null;

        var (than, loi) = DocVanBan(muc);

        if (loi is not null) return null;

        try
        {
            var goc = JsonDocument.Parse(than!).RootElement;

            if (goc.ValueKind != JsonValueKind.Object) return null;

            return new KhaiBaoGoi(
                Chuoi(goc, "phienBan"), Chuoi(goc, "diemCuoi"), Chuoi(goc, "bucket"),
                Chuoi(goc, "tienTo"), Chuoi(goc, "taoLuc"));
        }
        catch (JsonException)
        {
            return null;
        }

        static string? Chuoi(JsonElement doi, string ten) =>
            doi.TryGetProperty(ten, out var giaTri) && giaTri.ValueKind == JsonValueKind.String
                ? giaTri.GetString()
                : null;
    }

    private static string MoTaNguonToiThieu(string tenGoi) => $"gói «{tenGoi}»";

    private static string MoTaNguon(string tenGoi, KhaiBaoGoi? khaiBao)
    {
        if (khaiBao is null)
            return MoTaNguonToiThieu(tenGoi) + " — gói không khai địa chỉ kho (thiếu manifest hoặc "
                   + "manifest không đọc được)";

        var diaChi = string.IsNullOrWhiteSpace(khaiBao.DiemCuoi) || string.IsNullOrWhiteSpace(khaiBao.Bucket)
            ? "kho không rõ địa chỉ"
            : $"{khaiBao.DiemCuoi!.TrimEnd('/')}/{khaiBao.Bucket}"
              + (string.IsNullOrEmpty(khaiBao.TienTo) ? string.Empty : $"/{khaiBao.TienTo}");

        var luc = string.IsNullOrWhiteSpace(khaiBao.TaoLuc) ? string.Empty : $", tải lúc {khaiBao.TaoLuc}";

        var la = string.IsNullOrWhiteSpace(khaiBao.PhienBan) || khaiBao.PhienBan == PhienBanChuan
            ? string.Empty
            : $" — gói khai phiên bản khuôn lạ «{khaiBao.PhienBan}», công cụ vẫn bóc theo khuôn {PhienBanChuan}";

        // "theo gói khai" không phải chữ thừa: địa chỉ kho ở đây là lời của script, công cụ không
        // có cách nào kiểm nó — khác hẳn địa chỉ ở đường đọc thẳng, nơi chính tab đã gọi tới đó.
        return $"{MoTaNguonToiThieu(tenGoi)} — {diaChi}{luc} (theo gói khai){la}";
    }

    private static string MoTaThua(IReadOnlyList<string> thua) =>
        thua.Count == 0
            ? string.Empty
            : $" — gói còn {thua.Count.ToString(CultureInfo.InvariantCulture)} object không có trong "
              + $"trang danh sách nên KHÔNG được đưa vào kiểm (đầu tiên: «{thua[0]}»)";

    // ── Đọc mục trong gói ───────────────────────────────────────────────────────────────────

    private static (byte[]? Byte, string? Loi) DocByte(ZipArchiveEntry muc)
    {
        if (muc.Length > GioiHanByteMotMuc)
            return (null, $"mục lớn hơn trần {GioiHanByteMotMuc / (1024 * 1024)} MB");

        try
        {
            using var dong = muc.Open();
            using var bo = new MemoryStream();
            dong.CopyTo(bo);

            return (bo.ToArray(), null);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            return (null, ex.Message);
        }
    }

    private static (string? Than, string? Loi) DocVanBan(ZipArchiveEntry muc)
    {
        var (byteMuc, loi) = DocByte(muc);

        return loi is null ? (Encoding.UTF8.GetString(byteMuc!), null) : (null, loi);
    }
}
