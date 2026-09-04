using Noxh.XacMinh.Core.Kho;

namespace Noxh.XacMinh.Web.Kho;

/// <summary>Cách lấy trail về — tách ra để trang không dính chặt vào một cách gọi kho cụ thể.</summary>
public delegate Task<KhoBangChung> DocTrail(ThongSoKho kho, KhoaKho? khoa, CancellationToken huy);

/// <summary>
/// Đường ra mạng thứ hai của công cụ, và là đường duy nhất có mang khoá đi: đọc kho bằng chứng do
/// ban tổ chức công bố. Vẫn <b>không</b> hỏi máy chủ bốc thăm — kho là nơi bằng chứng đã nằm ngoài
/// tầm với của chính hệ thống bị nghi ngờ, nên đọc nó không làm yếu lập luận kiểm chứng độc lập.
///
/// Chỉ phát <c>GET</c>: request do <see cref="YeuCauDocKho"/> dựng và <see cref="SigV4"/> ký, mà
/// hàm ký từ chối ký mọi phương thức ghi — người kiểm có dán nhầm khoá có quyền ghi thì công cụ
/// cũng không dựng nổi một request sửa kho.
///
/// Mọi kiểu hỏng — mạng đứt, CORS chặn, kho từ chối, quá hạn chờ — đều trở thành một
/// <see cref="KhoBangChung"/> mang <see cref="KhoBangChung.Loi"/>, vì lõi cần "đọc không được" như
/// một dữ kiện để ra CHƯA ĐỦ DỮ LIỆU, chứ không phải một ngoại lệ bị nuốt.
/// </summary>
public sealed class TaiKhoBangChung(HttpClient http)
{
    /// <summary>Trần số lô tải về một lần: đủ cho một buổi lễ, và file nhầm không treo tab người dân.</summary>
    private const int GioiHanLo = 5000;

    public async Task<KhoBangChung> Doc(ThongSoKho kho, KhoaKho? khoa, CancellationToken huy = default)
    {
        var cheDo = khoa is { DuDeKy: true } ? CheDoDocKho.KhoaChiDoc : CheDoDocKho.AnDanh;

        KhoBangChung Hong(string loi) => KhoBangChung.Hong(cheDo, loi, kho.MoTa);

        if (!kho.DuDeDoc) return Hong("chưa nhập đủ địa chỉ kho (điểm cuối và tên bucket)");

        try
        {
            var (key, daLietKeHet, loi) = await LietKe(kho, khoa, huy);

            if (loi is not null) return Hong(loi);

            var doiTuong = new List<DoiTuongKho>(key.Count);
            foreach (var motKey in key) doiTuong.Add(await TaiMotLo(kho, khoa, motKey, huy));

            return KhoBangChung.Doc(cheDo, doiTuong, kho.MoTa, daLietKeHet);
        }
        catch (TaskCanceledException)
        {
            return Hong("hết thời gian chờ trả lời từ kho");
        }
        catch (HttpRequestException ex)
        {
            return Hong($"không gọi được kho (mạng hỏng hoặc trình duyệt chặn CORS): {ex.Message}");
        }
        catch (UriFormatException ex)
        {
            return Hong($"địa chỉ kho không hợp lệ: {ex.Message}");
        }
    }

    private async Task<(IReadOnlyList<string> Key, bool DaLietKeHet, string? Loi)> LietKe(
        ThongSoKho kho, KhoaKho? khoa, CancellationToken huy)
    {
        var key = new List<string>();
        string? dauTiepTuc = null;

        do
        {
            var yeu = YeuCauDocKho.LietKe(kho, khoa, dauTiepTuc, DateTimeOffset.UtcNow);
            using var traLoi = await Goi(yeu, huy);
            var than = await traLoi.Content.ReadAsStringAsync(huy);
            var trang = LietKeKho.Doc(than);

            // Kho từ chối trả về XML lỗi kèm mã: giữ nguyên mã đó, người kiểm cần phân biệt
            // "kho rỗng" với "khoá này không đọc được kho".
            if (trang.Loi is not null)
                return ([], false, traLoi.IsSuccessStatusCode ? trang.Loi : $"{trang.Loi} (HTTP {(int)traLoi.StatusCode})");

            if (!traLoi.IsSuccessStatusCode)
                return ([], false, $"kho trả HTTP {(int)traLoi.StatusCode}");

            key.AddRange(trang.Key);

            // Kho trả về cùng một dấu tiếp tục (hoặc trang rỗng mà vẫn báo còn nữa) thì vòng lặp này
            // treo tab người kiểm — dừng lại và khai là đọc chưa hết, đừng quay mãi.
            if (trang.DauTiepTuc is not null && trang.DauTiepTuc == dauTiepTuc)
                return (key, false, null);

            dauTiepTuc = trang.DauTiepTuc;

            if (key.Count >= GioiHanLo)
                return (key, false, null);
        }
        while (dauTiepTuc is not null);

        return (key, true, null);
    }

    private async Task<DoiTuongKho> TaiMotLo(ThongSoKho kho, KhoaKho? khoa, string key, CancellationToken huy)
    {
        try
        {
            using var traLoi = await Goi(YeuCauDocKho.Doc(kho, khoa, key, DateTimeOffset.UtcNow), huy);

            if (!traLoi.IsSuccessStatusCode)
                return new DoiTuongKho(key, [], $"tải nội dung không được: HTTP {(int)traLoi.StatusCode}");

            return new DoiTuongKho(key, await traLoi.Content.ReadAsByteArrayAsync(huy));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Một lô tải hỏng không được làm hỏng cả lần đọc: nó vào lõi như một lô "chưa đọc được",
            // và lõi biết đó không phải bằng chứng ai đó giấu lô.
            return new DoiTuongKho(key, [], $"tải nội dung không được: {ex.Message}");
        }
    }

    private Task<HttpResponseMessage> Goi(YeuCauHttp yeu, CancellationToken huy)
    {
        var request = new HttpRequestMessage(new HttpMethod(yeu.PhuongThuc), yeu.Url);

        foreach (var tieuDe in yeu.TieuDe) request.Headers.TryAddWithoutValidation(tieuDe.Ten, tieuDe.GiaTri);

        return http.SendAsync(request, huy);
    }
}
