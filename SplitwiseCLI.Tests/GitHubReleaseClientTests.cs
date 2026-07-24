using RichardSzalay.MockHttp;
using SplitwiseCLI.Update;
using Xunit;

namespace SplitwiseCLI.Tests;

public class GitHubReleaseClientTests
{
    [Fact]
    public async Task GetLatestReleaseAsync_ParsesTagNameBodyAndAssets()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://api.github.com/repos/CruelApe/SplitwiseCLI/releases/latest")
            .Respond("application/json", """
                {
                    "tag_name": "v1.1.0",
                    "name": "SplitwiseCLI v1.1.0",
                    "body": "Release notes here.",
                    "assets": [
                        {"name": "SplitwiseCLI-v1.1.0-win-x64.zip", "browser_download_url": "https://example.invalid/zip"},
                        {"name": "SHA256SUMS.txt", "browser_download_url": "https://example.invalid/sums"}
                    ]
                }
                """);

        var client = new GitHubReleaseClient(mockHttp.ToHttpClient());

        var release = await client.GetLatestReleaseAsync("CruelApe", "SplitwiseCLI");

        Assert.Equal("v1.1.0", release.TagName);
        Assert.Equal("Release notes here.", release.Body);
        Assert.Equal(2, release.Assets.Count);
        Assert.Contains(release.Assets, a => a.Name == "SplitwiseCLI-v1.1.0-win-x64.zip" && a.DownloadUrl == "https://example.invalid/zip");
    }

    [Fact]
    public async Task GetLatestReleaseAsync_SendsUserAgentHeader()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.Expect("https://api.github.com/repos/CruelApe/SplitwiseCLI/releases/latest")
            .With(req => req.Headers.UserAgent.Count > 0)
            .Respond("application/json", """{"tag_name":"v1.0.0","assets":[]}""");

        var client = new GitHubReleaseClient(mockHttp.ToHttpClient());

        await client.GetLatestReleaseAsync("CruelApe", "SplitwiseCLI");

        mockHttp.VerifyNoOutstandingExpectation();
    }
}
