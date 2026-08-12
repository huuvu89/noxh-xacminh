namespace Noxh.XacMinh.TestSupport;

/// <summary>
/// Định vị thư mục ở cây nguồn, không copy vào output: chỉ một bản duy nhất, không có bản sao cũ
/// trong bin/ che mất thay đổi.
/// </summary>
public static class RepoPaths
{
    public static string RepoRoot { get; } = FindDirContaining("fixtures");

    public static string FixturesDir { get; } = Path.Combine(RepoRoot, "fixtures");

    public static string Fixture(string fileName) => Path.Combine(FixturesDir, fileName);

    public static string Src(params string[] parts) =>
        Path.Combine(new[] { RepoRoot, "src" }.Concat(parts).ToArray());

    private static string FindDirContaining(string childName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, childName))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Không tìm thấy thư mục '{childName}/' khi đi ngược từ {AppContext.BaseDirectory}.");
    }
}
