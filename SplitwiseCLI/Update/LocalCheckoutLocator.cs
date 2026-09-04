namespace SplitwiseCLI.Update;

// Finds a local git clone of this project so 'update' can offer a real,
// automatic update for a GlobalTool install (whose files it otherwise can't
// safely touch) when it's actually run from inside that clone - the
// maintainer's own everyday case. Anywhere else, 'update' still falls back
// to just printing the manual pull/pack/reinstall instructions.
public static class LocalCheckoutLocator
{
    private static readonly string ProjectRelativePath = Path.Combine("SplitwiseCLI", "SplitwiseCLI.csproj");

    // Walks upward from `startDirectory` looking for a '.git' directory. Stops
    // at the first one found (a repo boundary), and only returns it when its
    // 'origin' remote (resolved via `getRemoteUrl`, injected so this stays
    // testable without shelling out to real git) matches `repositoryUrl` and
    // it actually contains this project - not just any repo that happens to
    // be an ancestor directory of the current one.
    public static string? Find(string startDirectory, string repositoryUrl, Func<string, string?> getRemoteUrl)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            if (!Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                continue;
            }

            var remoteUrl = getRemoteUrl(directory.FullName);
            var isMatch = remoteUrl is not null
                && UrlsReferSameRepository(remoteUrl, repositoryUrl)
                && File.Exists(Path.Combine(directory.FullName, ProjectRelativePath));
            return isMatch ? directory.FullName : null;
        }

        return null;
    }

    // Normalizes "git@github.com:Owner/Repo.git", "https://github.com/Owner/Repo.git"
    // and "https://github.com/Owner/Repo" all down to "github.com/Owner/Repo" so a
    // remote fetched from git can be compared against AppInfo.RepositoryUrl.
    public static bool UrlsReferSameRepository(string a, string b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string url)
    {
        var trimmed = url.Trim();

        if (trimmed.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex > 0)
            {
                trimmed = trimmed[(trimmed.IndexOf('@') + 1)..colonIndex] + "/" + trimmed[(colonIndex + 1)..];
            }
        }
        else
        {
            var schemeEnd = trimmed.IndexOf("://", StringComparison.Ordinal);
            if (schemeEnd >= 0)
            {
                trimmed = trimmed[(schemeEnd + 3)..];
            }
        }

        trimmed = trimmed.TrimEnd('/');
        if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        return trimmed;
    }
}
