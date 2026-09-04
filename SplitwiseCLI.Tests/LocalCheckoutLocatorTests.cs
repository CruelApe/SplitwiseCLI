using SplitwiseCLI.Update;
using Xunit;

namespace SplitwiseCLI.Tests;

public class LocalCheckoutLocatorTests
{
    private const string RepositoryUrl = "https://github.com/CruelApe/SplitwiseCLI";

    [Theory]
    [InlineData("https://github.com/CruelApe/SplitwiseCLI", "https://github.com/CruelApe/SplitwiseCLI")]
    [InlineData("https://github.com/CruelApe/SplitwiseCLI.git", "https://github.com/CruelApe/SplitwiseCLI")]
    [InlineData("git@github.com:CruelApe/SplitwiseCLI.git", "https://github.com/CruelApe/SplitwiseCLI")]
    [InlineData("https://github.com/CruelApe/SplitwiseCLI/", "https://github.com/CruelApe/SplitwiseCLI")]
    [InlineData("HTTPS://GITHUB.COM/CruelApe/SplitwiseCLI", "https://github.com/CruelApe/SplitwiseCLI")]
    public void UrlsReferSameRepository_ReturnsTrue_ForEquivalentUrls(string a, string b)
    {
        Assert.True(LocalCheckoutLocator.UrlsReferSameRepository(a, b));
    }

    [Theory]
    [InlineData("https://github.com/SomeoneElse/SplitwiseCLI", "https://github.com/CruelApe/SplitwiseCLI")]
    [InlineData("https://github.com/CruelApe/OtherRepo", "https://github.com/CruelApe/SplitwiseCLI")]
    [InlineData("https://gitlab.com/CruelApe/SplitwiseCLI", "https://github.com/CruelApe/SplitwiseCLI")]
    public void UrlsReferSameRepository_ReturnsFalse_ForDifferentRepositories(string a, string b)
    {
        Assert.False(LocalCheckoutLocator.UrlsReferSameRepository(a, b));
    }

    [Fact]
    public void Find_ReturnsRepoRoot_WhenGitDirectoryHasMatchingRemoteAndProject()
    {
        var root = CreateFakeCheckout();
        try
        {
            var startDirectory = Path.Combine(root, "SplitwiseCLI", "Cli");
            Directory.CreateDirectory(startDirectory);

            var found = LocalCheckoutLocator.Find(startDirectory, RepositoryUrl, _ => RepositoryUrl);

            Assert.Equal(root, found);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Find_ReturnsNull_WhenRemoteDoesNotMatch()
    {
        var root = CreateFakeCheckout();
        try
        {
            var found = LocalCheckoutLocator.Find(root, RepositoryUrl, _ => "https://github.com/SomeoneElse/SplitwiseCLI");

            Assert.Null(found);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Find_ReturnsNull_WhenProjectFileIsMissing()
    {
        var root = CreateFakeCheckout(withProjectFile: false);
        try
        {
            var found = LocalCheckoutLocator.Find(root, RepositoryUrl, _ => RepositoryUrl);

            Assert.Null(found);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Find_ReturnsNull_WhenNoGitDirectoryExistsAnywhereAbove()
    {
        var startDirectory = Path.Combine(Path.GetTempPath(), $"LocalCheckoutLocatorTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(startDirectory);
        try
        {
            // The temp directory itself has no '.git', and this stops climbing at
            // the first repo boundary it finds - the real risk here is climbing
            // all the way up to an unrelated ancestor repo and matching that
            // instead, so this only asserts the no-repo-at-all case is null too.
            var found = LocalCheckoutLocator.Find(startDirectory, RepositoryUrl, _ => RepositoryUrl);

            Assert.Null(found);
        }
        finally
        {
            Directory.Delete(startDirectory, recursive: true);
        }
    }

    private static string CreateFakeCheckout(bool withProjectFile = true)
    {
        var root = Path.Combine(Path.GetTempPath(), $"LocalCheckoutLocatorTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        if (withProjectFile)
        {
            Directory.CreateDirectory(Path.Combine(root, "SplitwiseCLI"));
            File.WriteAllText(Path.Combine(root, "SplitwiseCLI", "SplitwiseCLI.csproj"), "<Project />");
        }

        return root;
    }
}
