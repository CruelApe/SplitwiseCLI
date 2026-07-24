namespace SplitwiseCLI.Update;

public sealed record GitHubReleaseAsset(string Name, string DownloadUrl);

public sealed record GitHubRelease(string TagName, string Name, string Body, IReadOnlyList<GitHubReleaseAsset> Assets);
