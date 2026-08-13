using System.Globalization;
using Noxh.XacMinh.Core.Kho;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Hạng mục 10 và 11: lớp bằng chứng nằm <b>ngoài</b> cơ sở dữ liệu. Trong lúc lễ chạy, từng lô bản
/// ghi được đẩy thẳng lên kho lưu trữ chỉ-ghi bằng một khoá chỉ có quyền ghi thêm — chiếm được máy
/// chủ hay cơ sở dữ liệu cũng không xoá được thứ đã lên kho. Đó là lý do đọc trail đáng công: nó bắt
/// được đúng thứ mà chuỗi băm trong cơ sở dữ liệu không bắt được — sửa dữ liệu trong khoảng thời
/// gian trước khi chuỗi băm được vật chất hoá.
///
/// Hai câu hỏi tách làm hai hạng mục vì chúng có sức nặng khác nhau:
///  · <b>Chuỗi móc xích</b> — mỗi lô mang khoá và mã băm của lô liền trước, nên giấu bớt một lô ở
///    giữa là lô sau tố cáo ngay. Bắt được thì đó là KHÔNG ĐẠT thật sự.
///  · <b>Khoảng trống số thứ tự lô</b> — chỉ là cảnh báo: lô upload hỏng bị bỏ cũng để lại khoảng
///    trống y hệt lô bị giấu. Và dù liền mạch tới đâu, <b>phần đuôi bị cắt cụt vẫn không phát hiện
///    được</b>: không ai chứng minh được lô cuối cùng là lô cuối cùng.
///
/// Việc đọc kho (ký request, gọi mạng) là của vỏ UI; ở đây các lô đã đọc vào lõi dưới dạng dữ liệu.
/// </summary>
internal static class TrailBangChungCheck
{
    private const string TenChuoi = "Trail bằng chứng — chuỗi móc xích giữa các lô";

    private const string TenKhoangTrong = "Trail bằng chứng — khoảng trống số thứ tự lô";

    /// <summary>Giới hạn phải nói ở mọi kết luận, kể cả kết luận đẹp nhất — im lặng ở đây là ru ngủ.</summary>
    private const string GioiHanCatCut =
        "Lưu ý giới hạn của phép kiểm này: phần đuôi bị cắt cụt thì KHÔNG phát hiện được. Ai đó chặn không cho các "
        + "lô cuối lên kho thì chẳng có gì lộ ra, vì không ai chứng minh được lô cuối cùng là lô cuối cùng. "
        + "Trail chỉ chứng minh những gì ĐÃ lên kho thì không bị sửa, không chứng minh được thứ chưa bao giờ lên kho.";

    private const string ChuaDoc =
        "Chưa đọc kho bằng chứng, nên chưa có lô nào để kiểm. Trong lễ, kho chưa mở công khai nên cần khoá chỉ-đọc "
        + "do ban tổ chức cấp; sau lễ kho mở, đọc ẩn danh là được.";

    public static IEnumerable<CheckResult> Run(VerificationInput input)
    {
        yield return KiemChuoi(input.Kho);
        yield return KiemKhoangTrong(input.Kho);
    }

    // ── Chuỗi móc xích ──────────────────────────────────────────────────────────────────────

    private static CheckResult KiemChuoi(KhoBangChung? kho)
    {
        var soLieu = SoLieuChung(kho);

        CheckResult ChuaKiemDuoc(string vi) =>
            new(CheckIds.TrailChuoiLo, TenChuoi, CheckStatus.KhongKiemDuoc, vi) { Metrics = soLieu };

        if (kho is null) return ChuaKiemDuoc($"{ChuaDoc} {GioiHanCatCut}");

        if (kho.Loi is not null)
            return ChuaKiemDuoc(
                $"Không đọc được kho bằng chứng: {kho.Loi}. Đọc không được không phải bằng chứng gian lận, cũng "
                + "không phải cớ để bỏ qua — hãy kiểm lại địa chỉ kho, và nếu kho báo từ chối truy cập thì khoá bạn "
                + "đang dán không đọc được kho này (khoá chỉ-ghi của máy chủ đẩy bằng chứng cũng không đọc được, "
                + $"đúng như thiết kế). {GioiHanCatCut}");

        if (kho.Lo.Count == 0)
            return ChuaKiemDuoc(
                "Kho đọc được nhưng chưa có lô bằng chứng nào ở tiền tố này — có thể lễ chưa bắt đầu đẩy bằng "
                + $"chứng, hoặc tiền tố nhập vào chưa đúng. Không có lô thì không có gì để kiểm. {GioiHanCatCut}");

        var theoKey = kho.Lo
            .GroupBy(l => l.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var khongBocDuoc = kho.Lo.Where(l => l.Loi is not null).ToList();
        var soMatXich = 0;

        foreach (var lo in kho.Lo.Where(l => l.Loi is null && l.KeyLoTruoc is not null))
        {
            if (!theoKey.TryGetValue(lo.KeyLoTruoc!, out var loTruoc))
            {
                // Danh sách còn dở thì "không thấy lô trước" chỉ là hệ quả của việc đọc thiếu — nói
                // có người giấu bằng chứng lúc này là vu oan.
                if (!kho.DaLietKeHet)
                    return ChuaKiemDuoc(
                        $"Lô «{lo.Key}» trỏ về lô «{lo.KeyLoTruoc}» mà danh sách đọc được không có, nhưng danh sách "
                        + "này vốn còn dở (chưa đọc hết kho) nên chưa kết luận được gì. Hãy đọc lại cho hết danh "
                        + $"sách rồi kiểm lại. {GioiHanCatCut}");

                return new CheckResult(
                    CheckIds.TrailChuoiLo,
                    TenChuoi,
                    CheckStatus.KhongDat,
                    $"Lô «{lo.Key}» khai lô liền trước của nó là «{lo.KeyLoTruoc}», nhưng lô đó KHÔNG có trong danh "
                    + "sách kho đang trả về. Kho bằng chứng là kho chỉ-ghi: lô đã lên thì không được biến mất. Một "
                    + "mắt xích thiếu ở giữa nghĩa là hoặc có người đã lấy bớt bằng chứng, hoặc bạn đang đọc thiếu "
                    + $"một phần kho (sai tiền tố, danh sách bị lọc). {GioiHanCatCut}",
                    Expected: lo.KeyLoTruoc,
                    Actual: "không có trong danh sách kho")
                {
                    Metrics = soLieu,
                };
            }

            // Lô trước có trong danh sách nhưng tải/bóc không được: mắt xích này chưa kiểm được, và
            // "chưa kiểm được" không bao giờ được biến thành lời buộc tội.
            if (loTruoc.Loi is not null) continue;

            if (!string.Equals(loTruoc.Sha256, lo.Sha256LoTruoc, StringComparison.OrdinalIgnoreCase))
                return new CheckResult(
                    CheckIds.TrailChuoiLo,
                    TenChuoi,
                    CheckStatus.KhongDat,
                    $"Lô «{lo.Key}» khai mã băm của lô liền trước «{loTruoc.Key}», nhưng băm lại chính nội dung lô "
                    + "đó trên kho ra một giá trị KHÁC. Nghĩa là nội dung lô trước đã đổi sau khi lô sau được đóng "
                    + "gói — bằng chứng đã lên kho mà còn đổi được thì mắt xích này không còn giá trị chứng minh. "
                    + $"{GioiHanCatCut}",
                    Expected: lo.Sha256LoTruoc,
                    Actual: loTruoc.Sha256)
                {
                    Metrics = soLieu,
                };

            soMatXich++;
        }

        if (khongBocDuoc.Count > 0)
            return ChuaKiemDuoc(
                $"Có {khongBocDuoc.Count} lô trên kho không bóc được nên chuỗi móc xích đứt quãng ở đó — chưa kết "
                + "luận được, chứ chưa phải bằng chứng có người sửa. Lô đầu tiên không bóc được: "
                + $"«{khongBocDuoc[0].Key}» ({khongBocDuoc[0].Loi}). {GioiHanCatCut}");

        if (soMatXich == 0)
            return ChuaKiemDuoc(
                $"Đọc được {kho.Lo.Count} lô nhưng chưa có mắt xích nào để kiểm: lô đầu của mỗi tiến trình đẩy bằng "
                + "chứng không trỏ về đâu cả (đó là điểm bắt đầu chuỗi). Phải có từ hai lô liên tiếp trở lên thì "
                + $"chuỗi móc xích mới nói lên điều gì. {GioiHanCatCut}");

        return new CheckResult(
            CheckIds.TrailChuoiLo,
            TenChuoi,
            CheckStatus.Dat,
            $"Đọc được {kho.Lo.Count} lô trên kho bằng chứng và kiểm {soMatXich} mắt xích: mỗi lô mang đúng khoá và "
            + "đúng mã băm của lô liền trước, băm lại nội dung thật trên kho thì khớp. Nghĩa là không lô nào ở giữa "
            + "bị lấy bớt hay bị sửa sau khi đã lên kho — kể cả người nắm máy chủ bốc thăm cũng không làm được điều "
            + $"đó, vì khoá máy chủ giữ chỉ có quyền ghi thêm. {GioiHanCatCut}",
            Expected: $"{soMatXich} mắt xích khớp",
            Actual: $"{soMatXich} mắt xích khớp")
        {
            Metrics = soLieu,
        };
    }

    // ── Khoảng trống số thứ tự lô ───────────────────────────────────────────────────────────

    private static CheckResult KiemKhoangTrong(KhoBangChung? kho)
    {
        var soLieu = SoLieuChung(kho);

        CheckResult ChuaKiemDuoc(string vi, string? thieu = null) =>
            new(CheckIds.TrailKhoangTrong, TenKhoangTrong, CheckStatus.KhongKiemDuoc, vi, Actual: thieu)
            {
                Metrics = soLieu,
            };

        if (kho is null) return ChuaKiemDuoc($"{ChuaDoc} {GioiHanCatCut}");

        if (kho.Loi is not null)
            return ChuaKiemDuoc($"Không đọc được kho bằng chứng: {kho.Loi}. {GioiHanCatCut}");

        if (kho.Lo.Count == 0)
            return ChuaKiemDuoc($"Chưa có lô bằng chứng nào ở tiền tố này để soi số thứ tự. {GioiHanCatCut}");

        if (!kho.DaLietKeHet)
            return ChuaKiemDuoc(
                "Danh sách lô đọc được còn dở (kho còn trang chưa đọc hết), nên số thứ tự thiếu ở đây chưa nói lên "
                + $"điều gì — phải đọc hết danh sách rồi mới kết luận được. {GioiHanCatCut}");

        if (kho.Lo.Any(l => l.SoLo is null))
            return ChuaKiemDuoc(
                "Có lô không đọc được số thứ tự của chính nó, nên không dò được khoảng trống. "
                + $"{GioiHanCatCut}");

        var thieu = new List<string>();

        foreach (var nhom in kho.Lo
                     .GroupBy(l => l.MaTienTrinh ?? "không rõ tiến trình", StringComparer.Ordinal)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var daCo = nhom.Select(l => l.SoLo!.Value).ToHashSet();
            var conThieu = Enumerable.Range(1, (int)daCo.Max())
                .Select(so => (long)so)
                .Where(so => !daCo.Contains(so))
                .ToList();

            if (conThieu.Count > 0)
                thieu.Add($"{nhom.Key}: thiếu lô số "
                          + string.Join(", ", conThieu.Select(so => so.ToString(CultureInfo.InvariantCulture))));
        }

        var soTienTrinh = kho.Lo.Select(l => l.MaTienTrinh).Distinct(StringComparer.Ordinal).Count();

        if (thieu.Count > 0)
            return ChuaKiemDuoc(
                $"Số thứ tự lô KHÔNG liên tục — {string.Join("; ", thieu)}. Khoảng trống này có hai cách giải thích "
                + "và công cụ không tự chọn hộ: lô đó upload hỏng nên máy chủ bỏ (chuyện bình thường, chuỗi móc "
                + "xích vẫn liền vì lô sau trỏ về lô cuối lên kho thành công), hoặc lô đó đã bị chặn/lấy đi. Hãy "
                + "đối chiếu số vé trên trail với số vé trong báo cáo và với biên bản buổi lễ trước khi kết luận. "
                + $"{GioiHanCatCut}",
                string.Join("; ", thieu));

        return new CheckResult(
            CheckIds.TrailKhoangTrong,
            TenKhoangTrong,
            CheckStatus.Dat,
            $"Số thứ tự lô liên tục từ lô số 1 tới hết, trên cả {soTienTrinh} tiến trình đã đẩy bằng chứng — không "
            + "có lô nào ở giữa bị thiếu số. "
            + GioiHanCatCut,
            Expected: "liên tục từ lô số 1",
            Actual: "không thiếu số nào")
        {
            Metrics = soLieu,
        };
    }

    private static List<CheckMetric> SoLieuChung(KhoBangChung? kho)
    {
        if (kho is null) return [new CheckMetric("Kho bằng chứng", "chưa đọc")];

        var soLieu = new List<CheckMetric>
        {
            new("Chế độ đọc kho", kho.MoTaCheDo),
            new("Số lô đọc được", kho.Lo.Count.ToString(CultureInfo.InvariantCulture)),
            new("Số bản ghi trong các lô",
                kho.Lo.Sum(l => l.BanGhi.Count).ToString(CultureInfo.InvariantCulture)),
        };

        if (kho.MoTaNguon is not null) soLieu.Insert(0, new CheckMetric("Địa chỉ kho", kho.MoTaNguon));

        if (kho.Lo.Count > 0)
        {
            soLieu.Add(new CheckMetric("Lô đầu tiên", kho.Lo[0].Key));
            soLieu.Add(new CheckMetric("Lô cuối cùng", kho.Lo[^1].Key));
        }

        return soLieu;
    }
}
