namespace Noxh.XacMinh.TestSupport;

/// <summary>Fixture chuẩn vàng (vé #1) — nguồn dữ liệu thật cho mọi test kiểm chứng.</summary>
public static class GoldenFixture
{
    public const string FileName = "transparency-golden.json";

    /// <summary>Danh mục căn của <b>chính dự án trong fixture</b> — bản nhúng sẵn là dự án khác.</summary>
    public const string UnitCatalogFileName = "apartment-units-golden.json";

    /// <summary>Bảng danh sách hồ sơ đã khoá của chính dự án trong fixture (vé #16).</summary>
    public const string DanhSachFileName = "danh-sach-golden.tsv";

    public const string DanhSachMetaFileName = "danh-sach-golden.meta.json";

    public static string Path => RepoPaths.Fixture(FileName);

    public static string UnitCatalogPath => RepoPaths.Fixture(UnitCatalogFileName);

    public static string DanhSachPath => RepoPaths.Fixture(DanhSachFileName);

    /// <summary>Bảng danh sách đúng như người kiểm dán vào — đọc thô, không chuẩn hoá gì.</summary>
    public static string DanhSach() => File.ReadAllText(DanhSachPath);

    /// <summary>
    /// Khoá chỉ mục mù đã sinh ra bảng này. Là khoá dev mặc định của backend, công khai trong mã
    /// nguồn — khoá thật không bao giờ nằm trong repo này.
    /// </summary>
    public static string DanhSachKhoaHex() => DanhSachMeta("kIdxHex");

    /// <summary>Mã băm danh sách backend đã ghim cho chính bảng này.</summary>
    public static string DanhSachListHash() => DanhSachMeta("listHash");

    private static string DanhSachMeta(string field)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(RepoPaths.Fixture(DanhSachMetaFileName)));

        return doc.RootElement.GetProperty(field).GetString()!;
    }

    /// <summary>Đọc như trình duyệt đọc file người dùng thả vào: byte thô, tự giải mã UTF-8.</summary>
    public static byte[] Bytes() => File.ReadAllBytes(Path);

    public static string Json() => File.ReadAllText(Path);

    public static byte[] UnitCatalogBytes() => File.ReadAllBytes(UnitCatalogPath);
}
