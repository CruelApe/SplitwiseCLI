using System.Net.Http.Headers;
using System.Text.Json;

namespace SplitwiseCLI.Update;

// Talks to GitHub's public releases API - unrelated to Splitwise's own API,
// so this is deliberately a separate small client rather than folded into
// ISplitwiseClient/SplitwiseClient.
public sealed class GitHubReleaseClient
{
    private readonly HttpClient _httpClient;

    public GitHubReleaseClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        // GitHub's API rejects requests with no User-Agent header.
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SplitwiseCLI", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<GitHubRelease> GetLatestReleaseAsync(string owner, string repo, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"repos/{owner}/{repo}/releases/latest", cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = document.RootElement;

        var assets = root.GetProperty("assets").EnumerateArray()
            .Select(a => new GitHubReleaseAsset(
                a.GetProperty("name").GetString() ?? "",
                a.GetProperty("browser_download_url").GetString() ?? ""))
            .ToList();

        return new GitHubRelease(
            root.GetProperty("tag_name").GetString() ?? "",
            root.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
            assets);
    }
}
