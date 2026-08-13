using System.Reflection;

namespace Noxh.XacMinh.Core.Units;

/// <summary>
/// Danh mục căn nhúng sẵn trong công cụ (bản do ban tổ chức công bố), để mở là chạy được phần tái
/// lập chứ không phải đi tìm file. Người kiểm không phải tin bản này: màn hình hiện mã băm của nó
/// và cho nạp file khác đè lên.
/// </summary>
public static class EmbeddedUnitCatalog
{
    private const string TenTaiNguyen = "Noxh.XacMinh.Core.Units.apartment-units.json";

    private static readonly Lazy<UnitCatalog> DaNap = new(Nap);

    public static UnitCatalog Value => DaNap.Value;

    /// <summary>Byte nguyên bản của bản nhúng — cùng thứ mà mã băm được tính trên đó.</summary>
    public static byte[] Bytes()
    {
        using var stream = typeof(EmbeddedUnitCatalog).Assembly.GetManifestResourceStream(TenTaiNguyen)
            ?? throw new InvalidOperationException($"Bản dựng thiếu tài nguyên nhúng '{TenTaiNguyen}'.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return buffer.ToArray();
    }

    // Bản nhúng hỏng là lỗi bản dựng, không phải dữ liệu người dùng: ném ra để test đỏ ngay, chứ
    // không âm thầm chạy tiếp với một danh mục rỗng.
    private static UnitCatalog Nap()
    {
        var ketQua = UnitCatalogJson.Parse(Bytes());

        return ketQua.Catalog
               ?? throw new InvalidOperationException($"Danh mục nhúng không đọc được: {ketQua.ErrorMessage}");
    }
}
