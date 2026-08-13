using Noxh.XacMinh.Core.Kho;
using Noxh.XacMinh.Web.Kho;

namespace Noxh.XacMinh.Web.HienThi;

public enum BuocDocKho
{
    ChuaDoc,
    DangDoc,
    DaDoc,
}

/// <summary>
/// Việc đọc kho bằng chứng sống theo phiên và <b>chỉ chạy khi người kiểm bấm</b>. Ba điều lớp này
/// cam kết, và test là hàng rào:
///  · <b>Không giữ khoá.</b> Khoá chỉ-đọc đi thẳng từ ô nhập vào hàm ký rồi thôi — trạng thái phiên
///    chỉ giữ kết quả đọc được, nên không có chỗ nào để khoá nằm lại sau khi việc đã xong.
///  · <b>Đọc không được vẫn là đã đọc:</b> lỗi trở thành dữ kiện đi vào lõi, không biến mất.
///  · <b>Xoá được ngay</b>, và nạp báo cáo khác thì tự xoá — trail của dự án khác không làm chứng hộ.
/// </summary>
public sealed class TrangThaiKho(DocTrail doc)
{
    public KhoBangChung? DaDoc { get; private set; }

    public BuocDocKho Buoc { get; private set; } = BuocDocKho.ChuaDoc;

    /// <summary>Đã đọc cảnh báo về khoá chưa — chưa thì màn hình chưa mở ô dán khoá chỉ-đọc.</summary>
    public bool DaHieuCanhBao { get; private set; }

    public void HieuCanhBao() => DaHieuCanhBao = true;

    public async Task Doc(ThongSoKho thongSo, KhoaKho? khoa, CancellationToken huy = default)
    {
        if (Buoc == BuocDocKho.DangDoc) return;

        Buoc = BuocDocKho.DangDoc;
        DaDoc = null;

        try
        {
            DaDoc = await doc(thongSo, khoa, huy);
        }
        catch (Exception ex)
        {
            DaDoc = KhoBangChung.Hong(
                khoa is { DuDeKy: true } ? CheDoDocKho.KhoaChiDoc : CheDoDocKho.AnDanh,
                $"lỗi không lường trước: {ex.Message}",
                thongSo.MoTa);
        }
        finally
        {
            Buoc = BuocDocKho.DaDoc;
        }
    }

    public void Xoa()
    {
        DaDoc = null;
        Buoc = BuocDocKho.ChuaDoc;
    }
}
