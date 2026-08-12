namespace Noxh.XacMinh.Fixtures.Tests;

/// <summary>
/// Định vị <c>fixtures/</c> ở cây nguồn, không copy vào output: chỉ một bản duy nhất, không có
/// bản sao cũ trong bin/ che mất thay đổi.
/// </summary>
public static class RepoPaths
{
    public static string FixturesDir { get; } = FindFixturesDir();

    public static string Fixture(string fileName) => Path.Combine(FixturesDir, fileName);

    private static string FindFixturesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "fixtures");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Không tìm thấy thư mục 'fixtures/' khi đi ngược từ {AppContext.BaseDirectory}.");
    }
}
