using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.Web.MocNeo;

namespace Noxh.XacMinh.Web.HienThi;

public enum BuocTraCuuNeo
{
    ChuaTraCuu,
    DangTraCuu,
    DaTraCuu,
}

/// <summary>
/// Việc tra cứu mốc neo sống theo phiên và <b>chỉ chạy khi người dùng bấm</b>: cả trang cam kết
/// không gửi dữ liệu của người dùng đi đâu, nên bước có gọi mạng phải do người dùng chủ động, chứ
/// không lặng lẽ bắn request ngay khi nạp file.
/// </summary>
public sealed class TrangThaiMocNeo(DocKhoiCongKhai doc)
{
    private readonly List<QuanSatKhoi> daDoc = [];

    public IReadOnlyList<YeuCauTraCuuKhoi> CanDoc { get; private set; } = [];

    public IReadOnlyList<QuanSatKhoi> DaDoc => daDoc;

    public BuocTraCuuNeo Buoc { get; private set; } = BuocTraCuuNeo.ChuaTraCuu;

    /// <summary>Nạp báo cáo khác ⇒ quên block đã đọc: block của dự án khác không làm chứng hộ được.</summary>
    public void DatBaoCao(TransparencyReport? baoCao)
    {
        daDoc.Clear();
        Buoc = BuocTraCuuNeo.ChuaTraCuu;
        CanDoc = baoCao is null ? [] : MocNeoTraCuu.CanDoc(baoCao);
    }

    public async Task TraCuu(CancellationToken huy = default)
    {
        if (CanDoc.Count == 0 || Buoc == BuocTraCuuNeo.DangTraCuu) return;

        Buoc = BuocTraCuuNeo.DangTraCuu;
        daDoc.Clear();

        try
        {
            foreach (var yeu in CanDoc) daDoc.Add(await Hoi(yeu, huy));
        }
        finally
        {
            // Hỏi không được vẫn là đã tra cứu: kết quả "hỏi hỏng" phải đi vào lõi, không biến mất.
            Buoc = BuocTraCuuNeo.DaTraCuu;
        }
    }

    private async Task<QuanSatKhoi> Hoi(YeuCauTraCuuKhoi yeu, CancellationToken huy)
    {
        try
        {
            return await doc(yeu, huy);
        }
        catch (Exception ex)
        {
            return new QuanSatKhoi(yeu.ChuoiKhoi, yeu.DoCao, Loi: $"lỗi không lường trước: {ex.Message}");
        }
    }
}
