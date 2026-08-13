using Noxh.XacMinh.Core.DanhSach;

namespace Noxh.XacMinh.Web.HienThi;

/// <summary>
/// Bảng danh sách hồ sơ (dán tay hoặc file Excel gốc thả vào) và khoá chỉ mục mù tổ giám sát đưa
/// vào — dữ liệu nhạy cảm nhất đi qua công cụ này. Byte của file Excel cũng chỉ nằm ở đây, không
/// được tải lên đâu cả.
///
/// Bốn điều lớp này cam kết, và test là hàng rào:
///  · Chỉ nằm trong <b>bộ nhớ tab</b>: một trường trong một đối tượng theo phiên. Không
///    <c>localStorage</c>, không <c>sessionStorage</c>, không cookie, không file — nên đóng tab là
///    mất, và đó là đúng ý.
///  · Không đi đâu cả: hạng mục danh sách hồ sơ chạy hoàn toàn trong lõi thuần, không phát request
///    nào. Hai đường ra mạng của công cụ (tra cứu block, đọc kho bằng chứng) nằm ở chỗ khác và
///    không nhìn thấy dữ liệu này.
///  · Người dùng phải <b>xác nhận đã đọc cảnh báo</b> trước khi ô dán khoá xuất hiện: khoá chỉ mục
///    mù mở được cả cơ chế chỉ mục mù của hệ thống thật, dán nhầm chỗ là chuyện lớn.
///  · Xoá được ngay, và nạp báo cáo khác thì tự xoá — không giữ khoá của việc đã xong.
/// </summary>
public sealed class TrangThaiDanhSach
{
    public DanhSachDauVao? DaDan { get; private set; }

    /// <summary>Đã đọc cảnh báo về mức nhạy cảm của khoá chưa — chưa thì màn hình chưa mở ô dán khoá.</summary>
    public bool DaHieuCanhBao { get; private set; }

    /// <summary>Khoá đang dùng có phải dạng 64 ký tự hex hay không — để cảnh báo dán nhầm.</summary>
    public bool KhoaDangHex { get; private set; }

    public void HieuCanhBao() => DaHieuCanhBao = true;

    public void Dat(NguonBang nguon, string khoa)
    {
        DaDan = new DanhSachDauVao(nguon, khoa);
        KhoaDangHex = KhoaChiMuc.LaKhoaHex(khoa);
    }

    public void Dat(string bang, string khoa) => Dat(new NguonBang.Dan(bang), khoa);

    /// <summary>Xoá khoá và bảng khỏi bộ nhớ. Nạp báo cáo khác cũng gọi vào đây.</summary>
    public void Xoa()
    {
        DaDan = null;
        KhoaDangHex = false;
    }
}
