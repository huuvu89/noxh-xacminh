using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Hạng mục 6: <c>resultsHash == SHA-256(canonical(rows))</c> — bảng kết quả chung cuộc có đúng bản
/// đã ghim hay không. Đây là hạng mục duy nhất phủ được những dòng KHÔNG có vé nào: người ưu tiên
/// trực tiếp không phải bốc, và người được máy gom phân căn chỉ tồn tại trong bảng kết quả, nên sửa
/// những dòng đó không mâu thuẫn với nhật ký bốc — chuỗi băm nhật ký vẫn ĐẠT.
///
/// Mã băm chỉ tính trên các trường bất biến lúc niêm phong. Trường đổi hợp lệ sau lễ (giao kết quả,
/// huỷ kết quả) nằm ngoài canonical, nên huỷ một kết quả KHÔNG được làm hạng mục này đổi trạng thái
/// — mà dòng đã huỷ vẫn phải nằm trong phép băm, vì dòng kết quả không bao giờ bị xoá.
/// </summary>
internal static class ResultsHashCheck
{
    private const string Title = "Mã băm bảng kết quả";

    private const string DatGiaiThich =
        "Bảng kết quả chung cuộc đang công bố khớp mã băm đã ghim: không dòng nào bị sửa, thêm hay bớt — kể cả "
        + "những dòng không có lượt bốc nào (người thuộc diện ưu tiên trực tiếp không phải bốc, và người được máy "
        + "gom phân căn). Những thay đổi hợp lệ sau lễ như giao kết quả hay huỷ kết quả nằm ngoài mã băm, nên "
        + "chúng không làm hạng mục này đổi kết luận.";

    private const string KhongDatGiaiThich =
        "Mã băm tính lại từ bảng kết quả đang công bố KHÁC mã băm đã ghim: có dòng kết quả bị sửa (mã căn, hạng "
        + "dự khuyết, diện xét…), bị thêm hoặc bị bớt sau khi niêm phong, hoặc bảng đang công bố không phải bảng "
        + "đã ghim. Việc huỷ kết quả không gây ra sai lệch này vì nó nằm ngoài mã băm.";

    public static IEnumerable<CheckResult> Run(VerificationInput input)
    {
        yield return Kiem(input.Report.Results);
    }

    private static CheckResult Kiem(ResultTable? bang)
    {
        if (bang?.Rows is null)
            return new CheckResult(
                CheckIds.ResultsHash,
                Title,
                CheckStatus.KhongKiemDuoc,
                "Báo cáo không công bố bảng kết quả chung cuộc, nên không kiểm được bảng đang công bố có đúng bản "
                + "đã ghim mã băm hay không.");

        var dong = bang.Rows;
        var soLieu = SoLieu(dong);

        CheckResult ChuaKiemDuoc(string vi) =>
            new(CheckIds.ResultsHash, Title, CheckStatus.KhongKiemDuoc, vi,
                Expected: Hex.ChuanHoa(bang.ResultsHash))
            {
                Metrics = soLieu,
            };

        if (string.IsNullOrWhiteSpace(bang.ResultsHash))
            return ChuaKiemDuoc(
                "Bảng kết quả không công bố mã băm đã ghim, nên không có gì để đối chiếu với giá trị tính lại.");

        if (dong.Count == 0)
            return ChuaKiemDuoc(
                "Bảng kết quả không có dòng nào, nên không tính lại được mã băm của bảng để đối chiếu với giá trị "
                + "đã ghim.");

        var doc = dong.Select(Doc).ToList();

        if (doc.FirstOrDefault(d => d.Loi is not null) is { Loi: not null } thieu)
            return ChuaKiemDuoc(thieu.Loi);

        var canonical = doc.Select(d => d.Row!).ToList();
        var trung = canonical.GroupBy(r => r.ApplicantId).FirstOrDefault(g => g.Count() > 1);

        if (trung is not null)
        {
            soLieu.Add(new CheckMetric("Định danh hồ sơ lặp lại", trung.Key.ToString("D")));

            return new CheckResult(
                CheckIds.ResultsHash,
                Title,
                CheckStatus.KhongDat,
                $"Bảng kết quả có {trung.Count()} dòng cùng một định danh hồ sơ, mà mỗi hồ sơ chỉ có một dòng kết "
                + "quả: bảng đang công bố đã bị chèn thêm dòng sau khi niêm phong.",
                Expected: "1 dòng kết quả trên mỗi hồ sơ",
                Actual: $"{trung.Count()} dòng của hồ sơ {trung.Key:D}")
            {
                Metrics = soLieu,
            };
        }

        var tinhDuoc = ResultsCommitment.HashHex(canonical);
        var daGhim = bang.ResultsHash.Trim().ToLowerInvariant();
        var khop = string.Equals(tinhDuoc, daGhim, StringComparison.Ordinal);

        return new CheckResult(
            CheckIds.ResultsHash,
            Title,
            khop ? CheckStatus.Dat : CheckStatus.KhongDat,
            khop ? DatGiaiThich : KhongDatGiaiThich,
            Expected: daGhim,
            Actual: tinhDuoc,
            Preimage: ResultsCommitment.CanonicalText(canonical))
        {
            Metrics = soLieu,
        };
    }

    /// <summary>Một dòng đã đọc: hoặc đủ trường bất biến để băm lại, hoặc kèm câu giải thích vì sao không.</summary>
    private sealed record DaDoc(ResultsCommitment.Row? Row, string? Loi);

    private static DaDoc Doc(ResultRow r, int index)
    {
        var moTa = string.IsNullOrWhiteSpace(r.ApplicantId)
            ? $"thứ {index + 1} trong bảng"
            : $"của hồ sơ {r.ApplicantId.Trim()}";

        if (!Guid.TryParse(r.ApplicantId?.Trim(), out var applicantId))
            return new DaDoc(null, $"Dòng kết quả {moTa} "
                                   + (string.IsNullOrWhiteSpace(r.ApplicantId)
                                       ? "không công bố định danh hồ sơ"
                                       : "công bố định danh hồ sơ không hợp lệ")
                                   + ", mà định danh vừa nằm trong chuỗi đem băm vừa quyết định thứ tự các dòng, "
                                   + "nên không tính lại được mã băm của bảng.");

        if (r.Won is null)
            return new DaDoc(null, $"Dòng kết quả {moTa} không công bố kết quả trúng hay không, mà đó là một phần "
                                   + "của chuỗi đem băm, nên không tính lại được mã băm của bảng.");

        if (r.Tier is null)
            return new DaDoc(null, $"Dòng kết quả {moTa} không công bố diện xét, mà đó là một phần của chuỗi đem "
                                   + "băm, nên không tính lại được mã băm của bảng.");

        return new DaDoc(
            new ResultsCommitment.Row(applicantId, r.Won.Value, r.Tier, r.TypeCode, r.UnitCode, r.WaitlistRank),
            null);
    }

    /// <summary>Số liệu thô để đối chiếu với bảng kết quả đã công bố, không phải kết luận.</summary>
    private static List<CheckMetric> SoLieu(IReadOnlyList<ResultRow> dong) =>
    [
        new("Số dòng kết quả", dong.Count.ToString()),
        new("Số dòng trúng", dong.Count(r => r.Won == true).ToString()),
        new("Số dòng có mã căn", dong.Count(r => !string.IsNullOrWhiteSpace(r.UnitCode)).ToString()),
        new("Số dòng dự khuyết", dong.Count(r => r.WaitlistRank is not null).ToString()),
        new("Số dòng đã huỷ kết quả", dong.Count(r => !string.IsNullOrWhiteSpace(r.CancelledAt)).ToString()),
    ];
}
