namespace Noxh.XacMinh.TestSupport;

/// <summary>Fixture chuẩn vàng (vé #1) — nguồn dữ liệu thật cho mọi test kiểm chứng.</summary>
public static class GoldenFixture
{
    public const string FileName = "transparency-golden.json";

    /// <summary>Danh mục căn của <b>chính dự án trong fixture</b> — bản nhúng sẵn là dự án khác.</summary>
    public const string UnitCatalogFileName = "apartment-units-golden.json";

    public static string Path => RepoPaths.Fixture(FileName);

    public static string UnitCatalogPath => RepoPaths.Fixture(UnitCatalogFileName);

    /// <summary>Đọc như trình duyệt đọc file người dùng thả vào: byte thô, tự giải mã UTF-8.</summary>
    public static byte[] Bytes() => File.ReadAllBytes(Path);

    public static string Json() => File.ReadAllText(Path);

    public static byte[] UnitCatalogBytes() => File.ReadAllBytes(UnitCatalogPath);
}
