using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Transparency;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Phần dùng chung của mọi hạng mục phải <b>duyệt lại chuỗi băm nhật ký bốc</b>: đọc từng lượt bốc và
/// xếp chúng theo đúng thứ tự tất định của backend. Để chung một chỗ vì thứ tự này là thứ dễ trôi
/// nhất: hai hạng mục xếp khác nhau một chút là ra hai kết luận khác nhau trên cùng một file, mà
/// người đọc không có cách nào biết bên nào đúng.
/// </summary>
internal static class NhatKyBocChung
{
    /// <summary>
    /// Một lượt bốc sau khi đọc: hoặc đủ dữ liệu để tính lại (<see cref="Loi"/> rỗng), hoặc kèm câu
    /// giải thích vì sao không tính lại được. Khoá sắp xếp giữ riêng vì bước thiếu dữ liệu vẫn phải
    /// xếp được vào đúng chỗ trong chuỗi — bỏ nó ra khỏi hàng thì bước sau sẽ lệch oan.
    /// </summary>
    public sealed record Buoc(
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

    /// <summary>
    /// Đọc cả nhật ký rồi xếp theo thứ tự tất định của backend. Duyệt theo thứ tự này chứ không theo
    /// thứ tự mảng trong file: bản công bố đảo thứ tự vẫn phải ra cùng kết luận, và người sửa file
    /// không được chọn thứ tự có lợi.
    /// </summary>
    public static List<Buoc> DocVaSapXep(IReadOnlyList<DrawLogEntry> nhatKy)
    {
        var buoc = nhatKy.Select(Doc).ToList();
        buoc.Sort(TheoThuTuTatDinh);

        return buoc;
    }

    /// <summary>
    /// Mã băm tính lại của từng bước, theo đúng thứ tự chuỗi — phần tử thứ <c>i</c> là đầu chuỗi sau
    /// khi đã móc <c>i+1</c> bước. Tính lại từ chính các trường của bước, KHÔNG lấy mã băm đã công bố:
    /// lấy giá trị công bố thì một nhật ký bị sửa vẫn tự nhất quán với chính nó.
    /// Gọi khi mọi bước đều <see cref="Buoc.DocDuoc"/>; bước thiếu dữ liệu thì chuỗi đứt tại đó.
    /// </summary>
    public static List<string> TinhLaiChuoi(IReadOnlyList<Buoc> buoc)
    {
        var dauChuoi = new List<string>(buoc.Count);
        var prev = Array.Empty<byte>();

        foreach (var b in buoc)
        {
            prev = DrawLogHashChain.EntryHash(prev, b.ApplicantId, b.DeckId, b.Position, b.Payload);
            dauChuoi.Add(Convert.ToHexString(prev).ToLowerInvariant());
        }

        return dauChuoi;
    }

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
        + "CHƯA ĐỦ DỮ LIỆU thay vì ĐẠT.";
}
