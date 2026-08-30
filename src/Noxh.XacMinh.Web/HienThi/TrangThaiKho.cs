using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Kho;
using Noxh.XacMinh.Web.Kho;

namespace Noxh.XacMinh.Web.HienThi;

public enum BuocDocKho
{
    ChuaDoc,
    DangDoc,
    DaDoc,
}

/// <summary>Gói trail đã nạp trong phiên — mã băm tính trên đúng byte người kiểm thả vào.</summary>
public sealed record GoiDaNap(string TenFile, string MaBamSha256);

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

    /// <summary>
    /// Trail đang dùng đến từ gói nạp tay thì đây là gói đó, còn đọc thẳng kho thì <c>null</c>. Hai
    /// đường không được lẫn: bản xuất kết quả phải khai đúng byte nào đã đi vào lần kiểm này.
    /// </summary>
    public GoiDaNap? Goi { get; private set; }

    public void HieuCanhBao() => DaHieuCanhBao = true;

    public async Task Doc(ThongSoKho thongSo, KhoaKho? khoa, CancellationToken huy = default)
    {
        if (Buoc == BuocDocKho.DangDoc) return;

        Buoc = BuocDocKho.DangDoc;
        DaDoc = null;
        Goi = null;

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

    /// <summary>
    /// Nạp gói trail tải sẵn bằng script — đường thứ hai, dùng khi trình duyệt không gọi được kho
    /// (kho chưa bật CORS) hoặc máy đang chạy bản offline. Ở đây <b>không có mạng</b>: bóc gói là
    /// hàm thuần trong lõi, nên đường này không cần khoá và không gửi đi đâu cái gì.
    /// </summary>
    public void NapGoi(byte[] noiDung, string tenGoi)
    {
        // Băm trước khi bóc: bản xuất phải khai mã băm của đúng file người kiểm thả vào, kể cả khi
        // gói hỏng không bóc được — "đã thử kiểm bằng file này" cũng là một dữ kiện.
        Goi = new GoiDaNap(tenGoi, Hex.Sha256Hex(noiDung));
        DaDoc = DocGoiTrail.Doc(noiDung, tenGoi);
        Buoc = BuocDocKho.DaDoc;
    }

    public void Xoa()
    {
        DaDoc = null;
        Goi = null;
        Buoc = BuocDocKho.ChuaDoc;
    }
}
