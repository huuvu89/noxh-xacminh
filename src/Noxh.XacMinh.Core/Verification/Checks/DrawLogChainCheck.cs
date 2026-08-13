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

    private static CheckResult Kiem(IReadOnlyList<DrawLogEntry>? nhatKy)
    {
        if (nhatKy is null || nhatKy.Count == 0)
            return new CheckResult(
                CheckIds.DrawLogChain,
                Title,
                CheckStatus.KhongKiemDuoc,
                "Báo cáo không công bố nhật ký bốc, nên không tính lại được chuỗi băm để biết có lượt bốc nào "
                + "bị chèn, xoá hay sửa hay không.");

        return DoiChieu(NhatKyBocChung.DocVaSapXep(nhatKy));
    }

    private static CheckResult DoiChieu(List<NhatKyBocChung.Buoc> buoc)
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

    private static CheckResult ConNguyen(List<NhatKyBocChung.Buoc> buoc)
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

    private static CheckResult Hong(List<NhatKyBocChung.Buoc> buoc, int lech, byte[] preimage, string tinhDuoc)
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

    private static CheckResult ThieuDuLieu(List<NhatKyBocChung.Buoc> buoc, int dungTai) =>
        new(CheckIds.DrawLogChain, Title, CheckStatus.KhongKiemDuoc, buoc[dungTai].Loi!)
        {
            Metrics =
            [
                new CheckMetric("Số lượt bốc trong nhật ký", buoc.Count.ToString()),
                new CheckMetric("Số bước đã kiểm khớp trước chỗ thiếu", dungTai.ToString()),
            ],
        };
}
