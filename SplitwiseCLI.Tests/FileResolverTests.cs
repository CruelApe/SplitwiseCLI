using SplitwiseCLI.Import;
using Xunit;

namespace SplitwiseCLI.Tests;

public class FileResolverTests : IDisposable
{
    private readonly string _root;

    public FileResolverTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "SplitwiseCLI.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "nested"));
        File.WriteAllText(Path.Combine(_root, "a.xlsx"), "a");
        File.WriteAllText(Path.Combine(_root, "b.xlsx"), "b");
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "n");
        File.WriteAllText(Path.Combine(_root, "nested", "c.xlsx"), "c");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Resolve_LiteralFile_ReturnsThatFile()
    {
        var file = Path.Combine(_root, "a.xlsx");

        var result = FileResolver.Resolve(file);

        Assert.Single(result);
        Assert.Equal(Path.GetFullPath(file), result[0]);
    }

    [Fact]
    public void Resolve_Directory_ReturnsOnlyXlsxFilesNonRecursively()
    {
        var result = FileResolver.Resolve(_root);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.EndsWith(".xlsx", r, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result, r => r.Contains("nested", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_WildcardPattern_MatchesXlsxFilesInBaseDirectory()
    {
        var pattern = Path.Combine(_root, "*.xlsx").Replace('\\', '/');

        var result = FileResolver.Resolve(pattern);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Resolve_RecursiveWildcardPattern_MatchesNestedFiles()
    {
        var pattern = Path.Combine(_root, "**", "*.xlsx").Replace('\\', '/');

        var result = FileResolver.Resolve(pattern);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Resolve_NoMatches_ReturnsEmpty()
    {
        var pattern = Path.Combine(_root, "*.docx").Replace('\\', '/');

        var result = FileResolver.Resolve(pattern);

        Assert.Empty(result);
    }

    [Fact]
    public void Resolve_NonExistentLiteralPath_ReturnsEmpty()
    {
        var result = FileResolver.Resolve(Path.Combine(_root, "does-not-exist.xlsx"));

        Assert.Empty(result);
    }
}
