using Noxh.XacMinh.Core.Units;

namespace Noxh.XacMinh.Web.HienThi;

/// <summary>Bản danh mục đang dùng đến từ đâu — màn hình phải nói rõ, không để người kiểm đoán.</summary>
public enum NguonDanhMuc
{
    Nhung,
    NguoiDungNap,
}

/// <summary>
/// Danh mục căn đang dùng, sống theo phiên. File nạp hỏng <b>không</b> được phá bản đang dùng: người
/// kiểm chọn nhầm file thì mất luôn danh mục là kiểu hỏng khó chịu nhất, nên lỗi chỉ ra thông báo.
/// </summary>
public sealed class TrangThaiDanhMuc
{
    public UnitCatalog DanhMuc { get; private set; } = EmbeddedUnitCatalog.Value;

    public NguonDanhMuc Nguon { get; private set; } = NguonDanhMuc.Nhung;

    /// <summary>Tên file người dùng nạp, để màn hình nói rõ đang dùng bản nào.</summary>
    public string? TenFile { get; private set; }

    public string? Loi { get; private set; }

    public bool Nap(byte[]? noiDung, string tenFile)
    {
        var ketQua = UnitCatalogJson.Parse(noiDung);
        if (!ketQua.Success)
        {
            Loi = ketQua.ErrorMessage;
            return false;
        }

        DanhMuc = ketQua.Catalog!;
        Nguon = NguonDanhMuc.NguoiDungNap;
        TenFile = tenFile;
        Loi = null;

        return true;
    }

    /// <summary>Đọc file thất bại (quá lớn, trình duyệt từ chối) — vẫn là lỗi nạp, không phải lỗi dữ liệu.</summary>
    public void BaoLoi(string thongBao) => Loi = thongBao;

    public void DungBanNhung()
    {
        DanhMuc = EmbeddedUnitCatalog.Value;
        Nguon = NguonDanhMuc.Nhung;
        TenFile = null;
        Loi = null;
    }
}
