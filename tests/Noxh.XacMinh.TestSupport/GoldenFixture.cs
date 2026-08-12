namespace Noxh.XacMinh.TestSupport;

/// <summary>Fixture chuẩn vàng (vé #1) — nguồn dữ liệu thật cho mọi test kiểm chứng.</summary>
public static class GoldenFixture
{
    public const string FileName = "transparency-golden.json";

    public static string Path => RepoPaths.Fixture(FileName);

    /// <summary>Đọc như trình duyệt đọc file người dùng thả vào: byte thô, tự giải mã UTF-8.</summary>
    public static byte[] Bytes() => File.ReadAllBytes(Path);

    public static string Json() => File.ReadAllText(Path);
}
