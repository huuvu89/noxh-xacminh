using System.Globalization;
using System.Text.Json;
using Noxh.XacMinh.Core.Kho;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Phần dùng chung của ba hạng mục đối chiếu <b>trail bằng chứng ↔ báo cáo minh bạch</b>. Ba hạng mục
/// ấy là mỏ neo độc lập thứ ba của công cụ: chúng bắt được đúng thứ mà chuỗi băm trong cơ sở dữ liệu
/// không bắt được — dữ liệu bị sửa trong khoảng thời gian <b>trước khi</b> chuỗi băm được vật chất
/// hoá, vì bản đã nằm trên kho chỉ-ghi thì không sửa lại được nữa.
///
/// Ba chỗ dùng chung, và cả ba đều là chỗ dễ nói dối nếu mỗi hạng mục tự làm một kiểu:
///  · <b>Cửa vào</b> — chưa đọc kho, đọc không được, kho rỗng, hay trail của <b>dự án khác</b> thì
///    phải là KHÔNG KIỂM ĐƯỢC, không bao giờ là KHÔNG ĐẠT.
///  · <b>Giới hạn</b> — mọi kết luận, kể cả kết luận đẹp nhất, phải nói ra rằng trail không thấy
///    được thứ chưa bao giờ được đẩy lên.
///  · <b>Số liệu thô</b> — cùng một cách đếm, để ba hạng mục không nói ba con số khác nhau về cùng
///    một lần đọc kho.
/// </summary>
internal static class TrailDoiChieuChung
{
    public const string LoaiLuotBoc = "TICKET_DRAWN";

    public const string LoaiCamKet = "SECRET_COMMIT";

    public const string LoaiDauChuoi = "STEPCHAIN_HEAD";

    /// <summary>Giới hạn phải nói ở mọi kết luận — im lặng ở đây là ru ngủ người đọc.</summary>
    public const string GioiHan =
        "Giới hạn của phép đối chiếu này phải nói thẳng: trail chỉ chứng minh được những gì ĐÃ lên kho. Bản ghi "
        + "chưa bao giờ được đẩy lên thì không để lại dấu vết nào ở đây, nên đối chiếu cách mấy cũng không phát "
        + "hiện được — trail bịt đường sửa dữ liệu đã đẩy, không bịt đường không đẩy gì cả.";

    private const string ChuaDoc =
        "Chưa đọc kho bằng chứng, nên chưa có bản ghi nào để đối chiếu với báo cáo. Trong lễ, kho chưa mở công "
        + "khai nên cần khoá chỉ-đọc do ban tổ chức cấp; sau lễ kho mở, đọc ẩn danh là được.";

    /// <summary>Một bản ghi trên trail đã bóc phần payload; <see cref="Loi"/> khác null = payload không đọc được.</summary>
    public sealed record BanGhiDoiChieu(string LoKey, DateTimeOffset? XayRaLuc, JsonElement Payload, string? Loi);

    /// <summary>Các bản ghi một loại đọc được từ kho, đã lọc theo dự án của báo cáo đang xem.</summary>
    public sealed record TapBanGhi(IReadOnlyList<BanGhiDoiChieu> CuaDuAn, int SoCuaDuAnKhac)
    {
        public IEnumerable<BanGhiDoiChieu> DocDuoc => CuaDuAn.Where(b => b.Loi is null);

        public int SoKhongBocDuoc => CuaDuAn.Count(b => b.Loi is not null);
    }

    /// <summary>Lý do chưa đối chiếu được gì cả, hay <c>null</c> khi kho đã có dữ liệu để soi.</summary>
    public static string? CuaVao(KhoBangChung? kho)
    {
        if (kho is null) return $"{ChuaDoc} {GioiHan}";

        if (kho.Loi is not null)
            return $"Không đọc được kho bằng chứng: {kho.Loi}. Đọc không được không phải bằng chứng gian lận, cũng "
                   + "không phải cớ để bỏ qua — hãy kiểm lại địa chỉ kho và khoá đang dùng, rồi đọc lại. "
                   + GioiHan;

        if (kho.Lo.Count == 0)
            return "Kho đọc được nhưng chưa có lô bằng chứng nào ở tiền tố này, nên không có bản ghi nào để đối "
                   + $"chiếu với báo cáo. {GioiHan}";

        return null;
    }

    /// <summary>
    /// Bản ghi một loại, đã lọc theo dự án của báo cáo. Trail của dự án khác không làm chứng hộ:
    /// đem nó ra so với báo cáo này là so nhầm hai buổi lễ, và mọi kết luận sau đó đều vô nghĩa.
    /// </summary>
    public static TapBanGhi Doc(KhoBangChung kho, string loai, string? maDuAnBaoCao)
    {
        var cuaDuAn = new List<BanGhiDoiChieu>();
        var cuaDuAnKhac = 0;
        var maDuAn = maDuAnBaoCao?.Trim();

        foreach (var lo in kho.Lo)
        foreach (var ban in lo.BanGhi)
        {
            if (!string.Equals(ban.Loai, loai, StringComparison.Ordinal)) continue;

            if (!string.IsNullOrWhiteSpace(maDuAn) && !string.IsNullOrWhiteSpace(ban.MaDuAn)
                                                   && !string.Equals(ban.MaDuAn.Trim(), maDuAn,
                                                       StringComparison.OrdinalIgnoreCase))
            {
                cuaDuAnKhac++;
                continue;
            }

            cuaDuAn.Add(BocPayload(lo.Key, ban));
        }

        return new TapBanGhi(cuaDuAn, cuaDuAnKhac);
    }

    private static BanGhiDoiChieu BocPayload(string loKey, BanGhiTrail ban)
    {
        try
        {
            var payload = JsonDocument.Parse(ban.PayloadJson).RootElement.Clone();

            return payload.ValueKind == JsonValueKind.Object
                ? new BanGhiDoiChieu(loKey, ban.XayRaLuc, payload, null)
                : new BanGhiDoiChieu(loKey, ban.XayRaLuc, default, "nội dung bản ghi không phải một đối tượng JSON");
        }
        catch (JsonException ex)
        {
            return new BanGhiDoiChieu(loKey, ban.XayRaLuc, default, $"nội dung bản ghi không đọc được: {ex.Message}");
        }
    }

    /// <summary>Vì sao không có bản ghi loại này để đối chiếu — hai lý do rất khác nhau, không được gộp.</summary>
    public static string ViSaoKhongCoBanGhi(TapBanGhi tap, string moTaLoai) =>
        tap.SoCuaDuAnKhac > 0
            ? $"Kho đọc được {tap.SoCuaDuAnKhac} bản ghi {moTaLoai}, nhưng tất cả đều mang mã dự án KHÁC dự án của "
              + "báo cáo đang xem. Trail của dự án khác không làm chứng hộ dự án này — hãy kiểm lại tiền tố khoá "
              + $"object, hoặc kiểm lại xem báo cáo và kho có phải của cùng một buổi lễ không. {GioiHan}"
            : $"Trail đọc được không có bản ghi {moTaLoai} nào, nên không có gì để đối chiếu với báo cáo. Có thể "
              + "lễ chưa chạy tới chỗ sinh ra loại bản ghi này, hoặc đường đẩy bằng chứng lúc đó chưa bật. "
              + GioiHan;

    public static List<CheckMetric> SoLieu(KhoBangChung? kho, string moTaLoai, int soBanGhi)
    {
        if (kho is null) return [new CheckMetric("Kho bằng chứng", "chưa đọc")];

        var soLieu = new List<CheckMetric> { new("Chế độ đọc kho", kho.MoTaCheDo) };

        if (kho.MoTaNguon is not null) soLieu.Insert(0, new CheckMetric("Địa chỉ kho", kho.MoTaNguon));

        soLieu.Add(new CheckMetric("Số lô đọc được", kho.Lo.Count.ToString(CultureInfo.InvariantCulture)));
        soLieu.Add(new CheckMetric($"Số bản ghi {moTaLoai} trên trail",
            soBanGhi.ToString(CultureInfo.InvariantCulture)));

        return soLieu;
    }

    // ── Đọc trường trong payload: nội dung trên kho là thứ công cụ không kiểm soát được ─────

    public static string? Chuoi(JsonElement doi, string ten) =>
        doi.ValueKind == JsonValueKind.Object && doi.TryGetProperty(ten, out var giaTri)
        && giaTri.ValueKind == JsonValueKind.String
            ? giaTri.GetString()
            : null;

    public static long? So(JsonElement doi, string ten) =>
        doi.ValueKind == JsonValueKind.Object && doi.TryGetProperty(ten, out var giaTri)
        && giaTri.ValueKind == JsonValueKind.Number && giaTri.TryGetInt64(out var so)
            ? so
            : null;
}
