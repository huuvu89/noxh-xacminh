using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Hạng mục 4: chuỗi băm nhật ký bốc — <b>tính lại</b> mã băm từng bước từ đúng chuỗi đem băm
/// (mã băm bước trước ‖ định danh hồ sơ ‖ định danh chồng phiếu ‖ vị trí phiếu ‖ nội dung vé),
/// chứ không chỉ so bước sau có trỏ đúng bước trước hay không — kiểu so đó vẫn báo ĐẠT cho một
/// nhật ký đã bị sửa nội dung vé, nên chuỗi băm chỉ còn là trang trí.
///
/// Cả chuỗi là một mắt xích liên tục nên chỉ có một kết luận: hỏng ở bước nào thì từ đó về sau
/// không còn giá trị chứng minh. Bù lại phải nêu rõ bước lệch <b>đầu tiên</b> (vòng, vị trí).
/// </summary>
internal static class DrawLogChainCheck
{
    private const string Title = "Chuỗi băm nhật ký bốc";

    private const string DatGiaiThich =
        "Mã băm của từng lượt bốc được tính lại từ đúng chuỗi đem băm — mã băm bước trước, định danh hồ sơ, "
        + "định danh chồng phiếu, vị trí phiếu và nội dung vé — và khớp giá trị đã công bố: không có lượt bốc "
        + "nào bị chèn, xoá hay sửa ở giữa. (Chưa loại trừ việc cắt cụt phần đuôi nhật ký — cái đó cần đối "
        + "chiếu đầu chuỗi đã ghim dấu thời gian, là hạng mục riêng.)";

    public static IEnumerable<CheckResult> Run(VerificationInput input)
    {
        yield return Kiem(input.Report.DrawLog);
    }

    /// <summary>
    /// Một lượt bốc sau khi đọc: hoặc đủ dữ liệu để tính lại (<see cref="Loi"/> rỗng), hoặc kèm câu
    /// giải thích vì sao không tính lại được. Khoá sắp xếp giữ riêng vì bước thiếu dữ liệu vẫn phải
    /// xếp được vào đúng chỗ trong chuỗi — bỏ nó ra khỏi hàng thì bước sau sẽ lệch oan.
    /// </summary>
    private sealed record Buoc(
        string Round,
        Guid ApplicantId,
        Guid DeckId,
        int Position,
        string Payload,
        string EntryHash,
        string MoTa,
        string? Loi,
        int ThuTuVong)
    {
        public bool DocDuoc => Loi is null;
    }

    private static CheckResult Kiem(IReadOnlyList<DrawLogEntry>? nhatKy)
    {
        if (nhatKy is null || nhatKy.Count == 0)
            return new CheckResult(
                CheckIds.DrawLogChain,
                Title,
                CheckStatus.KhongKiemDuoc,
                "Báo cáo không công bố nhật ký bốc, nên không tính lại được chuỗi băm để biết có lượt bốc nào "
                + "bị chèn, xoá hay sửa hay không.");

        // Duyệt theo thứ tự tất định của backend chứ không theo thứ tự mảng trong file: bản công bố
        // đảo thứ tự vẫn phải ra cùng kết luận, và người sửa file không được chọn thứ tự có lợi.
        var buoc = nhatKy.Select(Doc).ToList();
        buoc.Sort(TheoThuTuTatDinh);

        return DoiChieu(buoc);
    }

    private static CheckResult DoiChieu(List<Buoc> buoc)
    {
        var prev = Array.Empty<byte>();

        for (var i = 0; i < buoc.Count; i++)
        {
            var b = buoc[i];

            // Bước không đọc được cắt chuỗi tại đây: mọi bước sau nó móc vào mã băm của nó, nên
            // không còn tính lại được. Bỏ qua nó rồi đi tiếp sẽ biến một chỗ THIẾU dữ liệu thành
            // một kết luận KHÔNG ĐẠT oan.
            if (!b.DocDuoc) return ThieuDuLieu(buoc, i);

            var preimage = DrawLogHashChain.Preimage(prev, b.ApplicantId, b.DeckId, b.Position, b.Payload);
            var tinhDuoc = Hex.Sha256Hex(preimage);

            if (!string.Equals(tinhDuoc, b.EntryHash, StringComparison.Ordinal))
                return Hong(buoc, i, preimage, tinhDuoc);

            // Bước sau móc vào mã băm ĐÃ TÍNH LẠI, không phải prevHash công bố — nếu không thì một
            // nhật ký bị sửa vẫn tự nhất quán với chính nó.
            prev = Convert.FromHexString(tinhDuoc);
        }

        return ConNguyen(buoc);
    }

    private static CheckResult ConNguyen(List<Buoc> buoc)
    {
        // Bước đầu tiên làm ví dụ tính tay được: preimage của nó không có mã băm bước trước.
        var dau = buoc[0];

        return new CheckResult(
            CheckIds.DrawLogChain,
            Title,
            CheckStatus.Dat,
            DatGiaiThich,
            Expected: dau.EntryHash,
            Actual: dau.EntryHash,
            Preimage: Convert.ToHexString(
                    DrawLogHashChain.Preimage([], dau.ApplicantId, dau.DeckId, dau.Position, dau.Payload))
                .ToLowerInvariant())
        {
            Metrics =
            [
                new CheckMetric("Số lượt bốc trong nhật ký", buoc.Count.ToString()),
                new CheckMetric("Bước lấy làm ví dụ preimage", dau.MoTa),
                new CheckMetric("Đầu chuỗi (mã băm bước cuối)", buoc[^1].EntryHash),
            ],
        };
    }

    private static CheckResult Hong(List<Buoc> buoc, int lech, byte[] preimage, string tinhDuoc)
    {
        var b = buoc[lech];

        return new CheckResult(
            CheckIds.DrawLogChain,
            Title,
            CheckStatus.KhongDat,
            $"Mã băm tính lại của lượt bốc thứ {lech + 1} ({b.MoTa}) KHÁC mã băm đã công bố: từ bước đó trở đi "
            + "nhật ký bốc không còn khớp chuỗi băm — có lượt bốc bị chèn, bị xoá, hoặc bị sửa nội dung sau khi "
            + "chuỗi được vật chất hoá.",
            Expected: b.EntryHash,
            Actual: tinhDuoc,
            Preimage: Convert.ToHexString(preimage).ToLowerInvariant())
        {
            Metrics =
            [
                new CheckMetric("Số lượt bốc trong nhật ký", buoc.Count.ToString()),
                new CheckMetric("Bước lệch đầu tiên", b.MoTa),
                new CheckMetric("Số bước còn khớp trước đó", lech.ToString()),
            ],
        };
    }

    private static CheckResult ThieuDuLieu(List<Buoc> buoc, int dungTai) =>
        new(CheckIds.DrawLogChain, Title, CheckStatus.KhongKiemDuoc, buoc[dungTai].Loi!)
        {
            Metrics =
            [
                new CheckMetric("Số lượt bốc trong nhật ký", buoc.Count.ToString()),
                new CheckMetric("Số bước đã kiểm khớp trước chỗ thiếu", dungTai.ToString()),
            ],
        };

    /// <summary>
    /// Thứ tự duyệt của backend: vòng → tên vòng → chồng phiếu → vị trí tăng dần. Backend còn có
    /// ràng buộc duy nhất (chồng phiếu, vị trí) nên bốn khoá đó là đủ; file người dùng thả vào thì
    /// KHÔNG — hai bước trùng khoá phải xếp theo một trật tự cố định, nếu không cùng một file lại
    /// cho hai kết luận khác nhau giữa hai lần chạy.
    /// </summary>
    private static int TheoThuTuTatDinh(Buoc a, Buoc b)
    {
        var theoVong = a.ThuTuVong.CompareTo(b.ThuTuVong);
        if (theoVong != 0) return theoVong;

        var theoTen = string.CompareOrdinal(a.Round, b.Round);
        if (theoTen != 0) return theoTen;

        var theoChongPhieu = a.DeckId.CompareTo(b.DeckId);
        if (theoChongPhieu != 0) return theoChongPhieu;

        var theoViTri = a.Position.CompareTo(b.Position);
        if (theoViTri != 0) return theoViTri;

        var theoHoSo = a.ApplicantId.CompareTo(b.ApplicantId);
        return theoHoSo != 0 ? theoHoSo : string.CompareOrdinal(a.EntryHash, b.EntryHash);
    }

    /// <summary>
    /// Đọc một lượt bốc. Bước thiếu dữ liệu vẫn nhận khoá sắp xếp từ những trường còn đọc được, và
    /// những trường không đọc được lấy giá trị NHỎ NHẤT — bước hỏng xếp sớm hơn chỗ thật của nó thì
    /// công cụ kiểm được ít hơn thực tế, còn xếp muộn hơn thì nó bỏ lọt một mắt xích đã đứt.
    /// </summary>
    private static Buoc Doc(DrawLogEntry e, int index)
    {
        var round = e.Round?.Trim();
        var position = e.Position;
        var moTa = position is { } vt && !string.IsNullOrWhiteSpace(round)
            ? $"vòng {round}, vị trí {vt}"
            : $"thứ {index + 1} trong nhật ký";

        Guid.TryParse(e.ApplicantId?.Trim(), out var applicantId);
        Guid.TryParse(e.DeckId?.Trim(), out var deckId);

        var buoc = new Buoc(
            round ?? string.Empty,
            applicantId,
            deckId,
            position ?? int.MinValue,
            e.Payload ?? string.Empty,
            Hex.ChuanHoa(e.EntryHash) ?? string.Empty,
            moTa,
            Loi: null,
            ThuTuVong: string.IsNullOrWhiteSpace(round) ? int.MinValue : DrawLogHashChain.ThuTuVong(round));

        var loi = ViSaoKhongDocDuoc(e, moTa);

        return loi is null ? buoc : buoc with { Loi = loi };
    }

    private static string? ViSaoKhongDocDuoc(DrawLogEntry e, string moTa)
    {
        if (!Guid.TryParse(e.ApplicantId?.Trim(), out _))
            return ThieuDinhDanh(moTa, "định danh hồ sơ", e.ApplicantId);

        if (!Guid.TryParse(e.DeckId?.Trim(), out _))
            return ThieuDinhDanh(moTa, "định danh chồng phiếu", e.DeckId);

        if (e.Position is null)
            return $"Lượt bốc {moTa} không công bố vị trí phiếu, mà vị trí là một phần của chuỗi đem băm, nên "
                   + "không tính lại được mã băm của bước này.";

        if (e.Payload is null)
            return $"Lượt bốc {moTa} không công bố nội dung vé, mà nội dung vé là một phần của chuỗi đem băm, "
                   + "nên không tính lại được mã băm của bước này.";

        if (Hex.Doc(e.EntryHash) is null)
            return $"Lượt bốc {moTa} không công bố mã băm hợp lệ của bước, nên không có gì để đối chiếu với "
                   + "giá trị tính lại.";

        return null;
    }

    private static string ThieuDinhDanh(string moTa, string ten, string? giaTri) =>
        $"Lượt bốc {moTa} "
        + (string.IsNullOrWhiteSpace(giaTri)
            ? $"không công bố {ten}"
            : $"công bố {ten} không phải một định danh hợp lệ")
        + $", mà {ten} là một phần bắt buộc của chuỗi đem băm. Thiếu nó thì chỉ so được bước sau có trỏ đúng "
        + "bước trước hay không — kiểu so đó vẫn báo ĐẠT cho một nhật ký đã bị sửa, nên ở đây phải kết luận "
        + "KHÔNG KIỂM ĐƯỢC thay vì ĐẠT.";
}
