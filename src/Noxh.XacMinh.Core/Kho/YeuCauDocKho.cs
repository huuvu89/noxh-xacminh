namespace Noxh.XacMinh.Core.Kho;

/// <summary>Một request HTTP đã dựng xong, chờ vỏ UI phát đi. Không có thân: công cụ chỉ đọc.</summary>
public sealed record YeuCauHttp(string PhuongThuc, string Url, IReadOnlyList<TieuDeYeuCau> TieuDe);

/// <summary>
/// Dựng request đọc kho bằng chứng theo kiểu <b>path-style</b> (<c>{điểm cuối}/{bucket}/{key}</c>) —
/// kho S3-compatible của bên tổ chức không cấp tên miền ảo cho từng bucket, và path-style là thứ
/// chắc chắn chạy ở cả hai nơi.
///
/// Hai chế độ, khác nhau đúng một chỗ: đọc ẩn danh <b>không gắn tiêu đề nào</b>, nên request là
/// request giản đơn — trình duyệt không phải hỏi preflight, bớt được đúng một chỗ CORS có thể chặn
/// người dân sau lễ. Đọc bằng khoá thì gắn <c>x-amz-date</c>, <c>x-amz-content-sha256</c> và chữ ký.
/// </summary>
public static class YeuCauDocKho
{
    public static YeuCauHttp LietKe(ThongSoKho kho, KhoaKho? khoa, string? dauTiepTuc, DateTimeOffset thoiDiem)
    {
        var thamSo = new List<ThamSoTruyVan> { new("list-type", "2") };

        if (!string.IsNullOrEmpty(kho.TienTo)) thamSo.Add(new ThamSoTruyVan("prefix", kho.TienTo));
        if (!string.IsNullOrEmpty(dauTiepTuc)) thamSo.Add(new ThamSoTruyVan("continuation-token", dauTiepTuc));

        return Dung(kho, khoa, $"/{kho.Bucket}", thamSo, thoiDiem);
    }

    public static YeuCauHttp Doc(ThongSoKho kho, KhoaKho? khoa, string key, DateTimeOffset thoiDiem) =>
        Dung(kho, khoa, $"/{kho.Bucket}/{key}", [], thoiDiem);

    private static YeuCauHttp Dung(
        ThongSoKho kho,
        KhoaKho? khoa,
        string duongDan,
        IReadOnlyList<ThamSoTruyVan> thamSo,
        DateTimeOffset thoiDiem)
    {
        var diemCuoi = kho.DiemCuoi.TrimEnd('/');
        var truyVan = SigV4.ChuoiTruyVan(thamSo);
        var url = diemCuoi + DuongDanDaMaHoa(duongDan) + (truyVan.Length == 0 ? string.Empty : $"?{truyVan}");

        if (khoa is null || !khoa.DuDeKy) return new YeuCauHttp("GET", url, []);

        var host = new Uri(diemCuoi).Authority;
        var tieuDe = new List<TieuDeYeuCau>
        {
            new("Host", host),
            new("x-amz-content-sha256", SigV4.MaBamThanRong),
            new("x-amz-date", thoiDiem.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'")),
        };

        if (!string.IsNullOrWhiteSpace(khoa.ThePhien))
            tieuDe.Add(new TieuDeYeuCau("x-amz-security-token", khoa.ThePhien!));

        var chuKy = SigV4.Ky(
            new YeuCauKy("GET", duongDan, thamSo, tieuDe, SigV4.MaBamThanRong, thoiDiem, kho.Vung, "s3"),
            khoa);

        // Host do trình duyệt tự đặt và không cho JS đặt lại — nó chỉ tham gia phần ký, không phát ra.
        return new YeuCauHttp(
            "GET",
            url,
            [.. tieuDe.Where(t => !string.Equals(t.Ten, "Host", StringComparison.OrdinalIgnoreCase)),
             new TieuDeYeuCau("Authorization", chuKy)]);
    }

    private static string DuongDanDaMaHoa(string duongDan) =>
        string.Join('/', duongDan.Split('/').Select(SigV4.MaHoa));
}
