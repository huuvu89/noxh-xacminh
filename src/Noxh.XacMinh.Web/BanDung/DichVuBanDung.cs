namespace Noxh.XacMinh.Web.BanDung;

public static class DichVuBanDung
{
    /// <summary>File nằm cạnh trang, đọc không ra trong ngần này giây thì thôi — chân trang không
    /// được giữ công cụ lại ở màn hình "đang tải".</summary>
    private static readonly TimeSpan HanCho = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Đọc dấu vết bản dựng đi kèm chính bản đang chạy. <b>Không phải đường ra mạng thứ ba</b>:
    /// đây là một file của chính bản dựng, cùng origin với trang, như <c>_framework/</c> — không
    /// có địa chỉ nào bên ngoài và không mang theo dữ liệu người dùng thả vào.
    /// Mọi kiểu hỏng đều thành <see cref="ThongTinBanDung.KhongRo"/>: nói "không biết" thì thật,
    /// còn ném ngoại lệ ở đây là làm trắng cả công cụ vì một dòng chân trang.
    /// </summary>
    public static async Task<ThongTinBanDung> Nap(string diaChiGoc)
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(diaChiGoc), Timeout = HanCho };
            using var traLoi = await http.GetAsync("build-info.json");
            if (!traLoi.IsSuccessStatusCode) return ThongTinBanDung.KhongRo;

            return ThongTinBanDung.TuJson(await traLoi.Content.ReadAsStringAsync())
                ?? ThongTinBanDung.KhongRo;
        }
        catch (Exception)
        {
            return ThongTinBanDung.KhongRo;
        }
    }
}
