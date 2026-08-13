using System.Globalization;
using System.Text;
using Noxh.XacMinh.Core.Verification;

namespace Noxh.XacMinh.Core.XuatKetQua;

/// <summary>
/// Bản xuất kết quả kiểm ra một file Markdown — thứ người ta trích dẫn, gửi đi, kẹp vào hồ sơ.
/// Nguyên tắc: file nói <b>đủ hơn</b> màn hình, không bao giờ ít hơn. Chế độ hiển thị là chuyện của
/// màn hình; file luôn mang đủ giá trị kỳ vọng, giá trị tính được và preimage để người khác tính
/// lại bằng công cụ của họ. Hạng mục KHÔNG KIỂM ĐƯỢC ở lại nguyên trạng thái đó — lược nó đi là
/// biến một file "chưa kết luận được" thành một file trông như đã đạt.
/// </summary>
public static class BanXuatVanBan
{
    public const string KieuNoiDung = "text/markdown";

    public static string TenFile(ThongTinBanXuat thongTin) =>
        "ket-qua-kiem-"
        + thongTin.ThoiDiemKiem.ToUniversalTime()
            .ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)
        + ".md";

    public static string Dung(VerificationReport baoCao, ThongTinBanXuat thongTin)
    {
        var ra = new StringBuilder();

        DungDauFile(ra, baoCao, thongTin);

        var thuTu = 0;
        foreach (var item in baoCao.Items)
        {
            DungHangMuc(ra, item, ++thuTu);
        }

        return ra.ToString();
    }

    private static void DungDauFile(StringBuilder ra, VerificationReport baoCao, ThongTinBanXuat thongTin)
    {
        ra.Append("# Kết quả kiểm chứng bốc thăm NƠXH\n\n");
        ra.Append($"- **Kết luận chung: {baoCao.Overall.Nhan()}**\n");

        if (!string.IsNullOrWhiteSpace(thongTin.Nguon))
        {
            ra.Append($"- Nguồn: {Markdown.MotDong(thongTin.Nguon)}\n");
        }

        ra.Append($"- Thời điểm kiểm: {ThoiDiem(thongTin.ThoiDiemKiem)}\n");
        ra.Append($"- Phiên bản công cụ: {Markdown.MotDong(thongTin.PhienBanCongCu)}\n");
        ra.Append($"- Số hạng mục: {TongHop(baoCao)}\n");

        ra.Append("- Dữ liệu đầu vào đã kiểm (SHA-256):\n");
        if (thongTin.DauVao.Count == 0)
        {
            ra.Append("  - (không ghi nhận được đầu vào nào)\n");
        }

        foreach (var dauVao in thongTin.DauVao)
        {
            ra.Append($"  - {Markdown.MotDong(dauVao.Ten)}: {Markdown.Ma(dauVao.MaBamSha256)}\n");
        }

        ra.Append(
            "\n> KHÔNG KIỂM ĐƯỢC là một kết luận, không phải một hạng mục bị bỏ qua: hạng mục đó thiếu "
            + "dữ liệu nên chưa kết luận được. Thiếu dữ liệu không có nghĩa là đạt.\n");

        ra.Append(
            "\nFile này do công cụ kiểm chứng độc lập sinh ra từ dữ liệu người kiểm tự nạp vào. Muốn "
            + "kiểm lại: tải đúng bản công cụ ghi ở trên, nạp lại đúng file có mã băm ghi ở trên.\n");
    }

    private static void DungHangMuc(StringBuilder ra, CheckResult item, int thuTu)
    {
        ra.Append($"\n## {thuTu}. {Markdown.MotDong(item.Title)} — {item.Status.Nhan()}\n\n");
        ra.Append($"{Markdown.MotDong(item.Explanation)}\n\n");
        ra.Append($"- Mã hạng mục: {Markdown.Ma(item.Id)}\n");

        // Thiếu giá trị vẫn phải in ra dòng nói rõ là thiếu — bỏ trống thì người đọc không phân biệt
        // được "công cụ không tính" với "công cụ tính ra rỗng".
        ra.Append($"- Giá trị kỳ vọng: {GiaTri(item.Expected, "(không công bố)")}\n");
        ra.Append($"- Giá trị tính được: {GiaTri(item.Actual, "(chưa tính được)")}\n");

        foreach (var soLieu in item.Metrics)
        {
            ra.Append($"- {Markdown.MotDong(soLieu.Label)}: {Markdown.MotDong(soLieu.Value)}\n");
        }

        if (item.Preimage is not null)
        {
            // Nguyên văn, không cắt: preimage bị cắt thì không ai băm lại ra được con số ở trên.
            ra.Append($"\nChuỗi đem băm (preimage):\n\n{Markdown.Khoi(item.Preimage, "text")}\n");
        }
    }

    private static string GiaTri(string? giaTri, string khiThieu) =>
        string.IsNullOrEmpty(giaTri) ? khiThieu : Markdown.Ma(giaTri);

    private static string ThoiDiem(DateTimeOffset luc) =>
        luc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string TongHop(VerificationReport baoCao)
    {
        var dat = baoCao.Items.Count(i => i.Status == CheckStatus.Dat);
        var khongDat = baoCao.Items.Count(i => i.Status == CheckStatus.KhongDat);
        var chuaKiem = baoCao.Items.Count(i => i.Status == CheckStatus.KhongKiemDuoc);

        return $"{baoCao.Items.Count} — {dat} ĐẠT · {khongDat} KHÔNG ĐẠT · {chuaKiem} KHÔNG KIỂM ĐƯỢC";
    }
}
