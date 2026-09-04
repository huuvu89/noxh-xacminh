using System.Globalization;
using System.Text;
using System.Text.Json;
using Noxh.XacMinh.Core.Verification;

namespace Noxh.XacMinh.Web.MocNeo;

/// <summary>Cách lấy một block về — tách ra để trang không dính chặt vào một nguồn công khai cụ thể.</summary>
public delegate Task<QuanSatKhoi> DocKhoiCongKhai(YeuCauTraCuuKhoi yeu, CancellationToken huy);

/// <summary>
/// Một trong hai chỗ trong công cụ nói chuyện với mạng (chỗ kia là đọc kho bằng chứng, xem
/// <c>Kho/TaiKhoBangChung</c>). Nó chỉ gửi đi một con số công khai (độ cao
/// block) và chỉ nhận về dữ liệu của sổ cái công khai — không đụng tới file người dùng thả vào, và
/// không bao giờ hỏi máy chủ bốc thăm (hệ thống đang bị nghi ngờ thì không được làm chứng cho chính
/// nó).
///
/// Mọi kiểu hỏng — mạng đứt, trình duyệt chặn CORS, nguồn trả rác, quá hạn chờ — đều trở thành một
/// <see cref="QuanSatKhoi"/> mang <see cref="QuanSatKhoi.Loi"/>, vì lõi cần "hỏi không được" như một
/// dữ kiện để ra CHƯA ĐỦ DỮ LIỆU kèm link tra cứu tay, chứ không phải một ngoại lệ bị nuốt.
/// </summary>
public sealed class TraCuuKhoiCongKhai(HttpClient http)
{
    public const string NguonEthereum = "ethereum-rpc.publicnode.com (JSON-RPC công khai)";

    public const string NguonBitcoin = "blockstream.info (API công khai)";

    private const string UrlEthereum = "https://ethereum-rpc.publicnode.com";

    private const string UrlBitcoin = "https://blockstream.info/api";

    public async Task<QuanSatKhoi> Doc(YeuCauTraCuuKhoi yeu, CancellationToken huy = default)
    {
        try
        {
            return yeu.ChuoiKhoi == ChuoiKhoiNeo.Bitcoin
                ? await DocBitcoin(yeu, huy)
                : await DocEthereum(yeu, huy);
        }
        catch (TaskCanceledException)
        {
            return Hong(yeu, "hết thời gian chờ trả lời từ nguồn công khai");
        }
        catch (HttpRequestException ex)
        {
            return Hong(yeu, $"không gọi được nguồn công khai (mạng hỏng hoặc trình duyệt chặn): {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Hong(yeu, $"nguồn công khai trả về dữ liệu không đọc được: {ex.Message}");
        }
    }

    private async Task<QuanSatKhoi> DocEthereum(YeuCauTraCuuKhoi yeu, CancellationToken huy)
    {
        var doCaoHex = "0x" + yeu.DoCao.ToString("x", CultureInfo.InvariantCulture);
        var than = $$"""{"jsonrpc":"2.0","id":1,"method":"eth_getBlockByNumber","params":["{{doCaoHex}}",false]}""";

        using var noiDung = new StringContent(than, Encoding.UTF8, "application/json");
        using var traLoi = await http.PostAsync(UrlEthereum, noiDung, huy);

        if (!traLoi.IsSuccessStatusCode)
            return Hong(yeu, $"nguồn công khai trả HTTP {(int)traLoi.StatusCode}", NguonEthereum);

        var doc = JsonSerializer.Deserialize(
            await traLoi.Content.ReadAsStringAsync(huy), MocNeoJsonContext.Default.EthTraLoi);

        if (doc?.Error?.Message is { } loi) return Hong(yeu, $"nguồn công khai báo lỗi: {loi}", NguonEthereum);
        if (doc?.Result?.Hash is null)
            return Hong(yeu, "nguồn công khai không có block nào ở độ cao này", NguonEthereum);

        // Nguồn trả block ở độ cao khác thứ đã hỏi thì coi như chưa đọc được: kết luận trên block
        // khác còn tệ hơn không kết luận.
        if (DocHex(doc.Result.Number) != yeu.DoCao)
            return Hong(yeu, "nguồn công khai trả về block ở độ cao khác độ cao đã hỏi", NguonEthereum);

        var giay = DocHex(doc.Result.Timestamp);

        return new QuanSatKhoi(
            yeu.ChuoiKhoi,
            yeu.DoCao,
            doc.Result.Hash,
            giay is null ? null : DateTimeOffset.FromUnixTimeSeconds(giay.Value),
            NguonEthereum);
    }

    private async Task<QuanSatKhoi> DocBitcoin(YeuCauTraCuuKhoi yeu, CancellationToken huy)
    {
        var doCao = yeu.DoCao.ToString(CultureInfo.InvariantCulture);
        var maBam = (await http.GetStringAsync($"{UrlBitcoin}/block-height/{doCao}", huy)).Trim();

        if (maBam.Length == 0) return Hong(yeu, "nguồn công khai không có block nào ở độ cao này", NguonBitcoin);

        var doc = JsonSerializer.Deserialize(
            await http.GetStringAsync($"{UrlBitcoin}/block/{Uri.EscapeDataString(maBam)}", huy),
            MocNeoJsonContext.Default.BlockstreamKhoi);

        if (doc?.Height is not null && doc.Height != yeu.DoCao)
            return Hong(yeu, "nguồn công khai trả về block ở độ cao khác độ cao đã hỏi", NguonBitcoin);

        return new QuanSatKhoi(
            yeu.ChuoiKhoi,
            yeu.DoCao,
            maBam,
            doc?.Timestamp is null ? null : DateTimeOffset.FromUnixTimeSeconds(doc.Timestamp.Value),
            NguonBitcoin);
    }

    private static QuanSatKhoi Hong(YeuCauTraCuuKhoi yeu, string loi, string? nguon = null) =>
        new(yeu.ChuoiKhoi, yeu.DoCao, Nguon: nguon, Loi: loi);

    /// <summary>Số kiểu Ethereum về dạng <c>0x1a</c>; đọc không ra thì để trống chứ không đoán.</summary>
    private static long? DocHex(string? giaTri)
    {
        if (string.IsNullOrWhiteSpace(giaTri)) return null;

        var text = giaTri.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];

        return long.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var so) ? so : null;
    }
}
