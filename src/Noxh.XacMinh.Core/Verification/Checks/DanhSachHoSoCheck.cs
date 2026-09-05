using System.Globalization;
using Noxh.XacMinh.Core.DanhSach;

namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>
/// Hạng mục 9: bảng danh sách hồ sơ tổ giám sát đang cầm có đúng là danh sách đã khoá và đã được
/// ghim dấu thời gian hay không. Đây là mảnh còn thiếu của toàn bộ lập luận công bằng: mọi hạng mục
/// khác chứng minh buổi bốc thăm chạy đúng trên <b>một</b> tập hồ sơ, còn hạng mục này mới nói tập
/// hồ sơ đó là tập nào.
///
/// Hạng mục duy nhất cần <b>khoá</b>: mã băm tính trên chỉ mục mù của số định danh, mà chỉ mục mù
/// là HMAC với <c>K_idx</c>. Không có khoá thì không ai — kể cả người có danh sách — dựng lại được
/// mã băm, đó là chủ ý: nếu không, mã băm công bố sẽ thành công cụ dò xem một người có trong danh
/// sách hay không.
///
/// Ba ranh giới của hạng mục này:
///  · KHÔNG chứng minh danh sách là "danh sách đúng" — ai đủ điều kiện dự bốc thăm là việc của hội
///    đồng xét duyệt. Chỉ chứng minh nó khớp cam kết đã ghim trước khi lễ bắt đầu.
///  · KHÔNG bao giờ tự sửa dữ liệu người kiểm dán vào cho khớp; lệch thì chẩn đoán và nói "khớp
///    nếu…", còn kết luận vẫn là chưa khớp.
///  · KHÔNG đưa chuỗi đem băm (toàn bộ họ tên + chỉ mục mù của cả danh sách) vào kết quả kiểm, kể
///    cả ở chế độ chuyên sâu — công cụ này để đối chiếu mã băm, không phải để bày danh sách người
///    dân ra màn hình rồi lọt vào ảnh chụp hay báo lỗi.
/// </summary>
internal static class DanhSachHoSoCheck
{
    private const string Ten = "Danh sách hồ sơ đầu vào";

    private const string YNghia =
        "Nghĩa là buổi bốc thăm chạy trên đúng tập hồ sơ này, chốt từ trước và có dấu thời gian của bên thứ ba — "
        + "không phải một danh sách được thêm bớt sau khi đã biết ai bốc được căn nào.";

    /// <summary>
    /// Lệch mã băm khi thả file gốc còn một nguyên nhân mà đường dán tay không có, và người kiểm
    /// cần biết trước khi kết luận: hệ thống loại bớt dòng ngay lúc nhập.
    /// </summary>
    private const string LuuYFileGoc =
        "Riêng với file Excel gốc còn một khả năng nữa: lúc nhập, hệ thống bỏ những dòng không hợp lệ (số định danh "
        + "hay số điện thoại sai định dạng, trùng mã hồ sơ, trùng số định danh, loại căn hộ không có trong dự án). "
        + "File gốc còn giữ các dòng đó thì danh sách đã khoá không có chúng, và mã băm lệch dù không ai sửa gì — "
        + "hãy đối chiếu số hồ sơ đọc được ở trên với số hồ sơ trong biên bản khoá danh sách.";

    public static IEnumerable<CheckResult> Run(VerificationInput input)
    {
        yield return Kiem(input);
    }

    private static CheckResult Kiem(VerificationInput input)
    {
        var soLieu = new List<CheckMetric>();

        CheckResult ChuaKiemDuoc(string vi, string? kyVong = null) =>
            new(CheckIds.DanhSachHoSo, Ten, CheckStatus.KhongKiemDuoc, vi, Expected: kyVong) { Metrics = soLieu };

        var (daCongBo, nguonMaBam) = DauThoiGianChung.MaBamDanhSachDaCongBo(input.Report);

        if (daCongBo is null)
            return ChuaKiemDuoc(
                "Báo cáo không công bố mã băm danh sách hồ sơ ở đâu cả — không ở trường riêng, cũng không trong "
                + "chuỗi đã được đóng dấu thời gian của mốc cam kết. Không có con số đã ghim thì không có gì để đối "
                + "chiếu danh sách, và một danh sách không đối chiếu được thì không nói lên điều gì.");

        soLieu.Add(new CheckMetric($"Mã băm danh sách đã ghim ({nguonMaBam})", daCongBo));

        if (input.DanhSach is null)
            return ChuaKiemDuoc(
                "Chưa nạp danh sách hồ sơ và khoá chỉ mục mù, nên chưa dựng lại được mã băm danh sách để đối chiếu. "
                + "Chọn hoặc kéo thả file Excel gốc đã dùng để nhập danh sách, hoặc dán bảng danh sách đã khoá. "
                + "Đây là hạng mục dành cho tổ giám sát — người có danh sách đã khoá và khoá chỉ mục mù trong tay.",
                daCongBo);

        var nguon = input.DanhSach.Nguon;
        soLieu.Add(new CheckMetric("Nguồn bảng danh sách", nguon.MoTa));

        var khoa = KhoaChiMuc.Doc(input.DanhSach.Khoa);

        if (khoa is null)
            return ChuaKiemDuoc(
                "Chưa dán khoá chỉ mục mù. Mã băm danh sách tính trên chỉ mục mù của số định danh, nên không có "
                + "khoá thì không dựng lại được — chính chỗ đó khiến mã băm công bố không thành công cụ dò xem một "
                + "người có tên trong danh sách hay không.",
                daCongBo);

        var bang = BangDanhSach.Doc(nguon);

        if (bang.HoSo.Count == 0)
            return ChuaKiemDuoc(
                $"Không đọc được hồ sơ nào từ {nguon.MoTa}. {HuongDanDocBang(nguon)}"
                + (bang.Loi.Count > 0 ? $" Chỗ hỏng đầu tiên: {bang.Loi[0]}." : string.Empty),
                daCongBo);

        soLieu.Add(new CheckMetric("Số hồ sơ đọc được", bang.HoSo.Count.ToString(CultureInfo.InvariantCulture)));

        if (bang.CoDongTieuDe)
            soLieu.Add(new CheckMetric("Dòng đầu bảng", "đã bỏ qua vì đọc là dòng tiêu đề"));

        foreach (var nhom in Enumerable.Range(0, NhomDoiTuong.SoNhom))
            soLieu.Add(new CheckMetric(
                NhomDoiTuong.Nhan(nhom),
                bang.HoSo.Count(h => h.Nhom == nhom).ToString(CultureInfo.InvariantCulture)));

        // Số hồ sơ + cơ cấu nhóm phải đọc được ở CẢ chế độ thường: đây là bộ số người kiểm đối chiếu
        // ngay với biên bản bàn giao danh sách, trước cả khi bàn tới mã băm.
        var moTaSoLieu =
            $"Đọc từ {nguon.MoTa}: {bang.HoSo.Count} hồ sơ — "
            + string.Join(", ", Enumerable.Range(0, NhomDoiTuong.SoNhom)
                .Select(n => $"{NhomDoiTuong.Ma(n)}: {bang.HoSo.Count(h => h.Nhom == n)}"))
            + ". Hãy đối chiếu ngay bộ số này với biên bản bàn giao danh sách. ";

        if (bang.Loi.Count > 0)
            return ChuaKiemDuoc(
                moTaSoLieu
                + $"Còn {bang.Loi.Count} dòng chưa đọc được nên bảng đang thiếu, và một bảng thiếu dòng thì chắc "
                + $"chắn lệch mã băm dù danh sách gốc không sai. Chỗ hỏng đầu tiên: {bang.Loi[0]}.",
                daCongBo);

        var tinhDuoc = MaBamDanhSach.Tinh(bang.HoSo, khoa);

        if (string.Equals(tinhDuoc, daCongBo, StringComparison.Ordinal))
            return new CheckResult(
                CheckIds.DanhSachHoSo,
                Ten,
                CheckStatus.Dat,
                moTaSoLieu
                + "Băm lại bảng này ra đúng mã băm danh sách đã ghim và đã được đóng dấu thời gian. " + YNghia,
                Expected: daCongBo,
                Actual: tinhDuoc)
            {
                Metrics = soLieu,
            };

        var bienThe = ChanDoanBang.BienTheKhop(bang.HoSo, khoa, daCongBo);

        // Lệch mà có một biến thể chuẩn hoá khớp thì gần như chắc chắn là vết sao chép bảng, không
        // phải danh sách bị thay: nói KHÔNG ĐẠT ở đây là vu cho ban tổ chức, mà nói ĐẠT thì lại là
        // công cụ tự sửa dữ liệu cho khớp. Chưa kết luận được, và nói rõ phải làm gì tiếp.
        if (bienThe is not null)
            return new CheckResult(
                CheckIds.DanhSachHoSo,
                Ten,
                CheckStatus.KhongKiemDuoc,
                moTaSoLieu
                + $"Băm lại bảng này KHÔNG ra mã băm đã ghim, nhưng khớp nếu {bienThe}. Đó là cách ghi khác nhau của "
                + "cùng một dữ liệu — dấu vết của việc sao chép bảng qua PDF/Word chứ không phải danh sách bị sửa. "
                + "Công cụ KHÔNG tự sửa bảng của bạn cho khớp: hãy lấy lại bản gốc từng byte của bảng danh sách rồi "
                + "dán lại, chừng nào chưa làm thì hạng mục này chưa kết luận được.",
                Expected: daCongBo,
                Actual: tinhDuoc)
            {
                Metrics = soLieu,
            };

        return new CheckResult(
            CheckIds.DanhSachHoSo,
            Ten,
            CheckStatus.KhongDat,
            moTaSoLieu
            + "Băm lại bảng này ra một mã băm KHÁC mã băm danh sách đã ghim, và không có biến thể chuẩn hoá thường "
            + "gặp nào (khoảng trắng, cách ghi dấu tiếng Việt, cách ghi số định danh, thứ tự sắp xếp) giải thích "
            + "được khoảng lệch đó. Nghĩa là bảng bạn đang cầm khác bản đã khoá ở chính dữ liệu — hoặc khoá chỉ mục "
            + "mù bạn dán không phải khoá đã dùng lúc khoá danh sách. Hãy xác nhận lại khoá trước khi kết luận về "
            + "danh sách."
            + (nguon is NguonBang.Excel ? " " + LuuYFileGoc : string.Empty),
            Expected: daCongBo,
            Actual: tinhDuoc)
        {
            Metrics = soLieu,
        };
    }

    /// <summary>Cách công cụ đọc bảng — nói theo đúng đường người kiểm vừa dùng, không nói chung chung.</summary>
    private static string HuongDanDocBang(NguonBang nguon) => nguon switch
    {
        NguonBang.Excel =>
            "Công cụ đọc trang tính đầu tiên, lấy dòng có dữ liệu đầu tiên làm dòng tiêu đề rồi nhận cột theo TÊN "
            + "tiêu đề — mã hồ sơ · họ tên · số định danh · nhóm đối tượng — đúng như hệ thống đã đọc lúc nhập danh "
            + "sách, nên thứ tự cột và các cột thừa không quan trọng. File lạ quá thì vẫn còn đường dán bảng.",
        _ =>
            "Bảng cần bốn cột theo đúng thứ tự biên bản: mã hồ sơ · họ tên · số định danh · nhóm đối tượng, mỗi hồ "
            + "sơ một dòng, các cột ngăn nhau bằng ký tự tab (dán thẳng từ bảng tính) hoặc bằng dấu «|».",
    };
}
