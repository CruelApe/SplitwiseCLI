using SplitwiseCLI.Update;
using Xunit;

namespace SplitwiseCLI.Tests;

public class VersionComparerTests
{
    [Theory]
    [InlineData("v1.1.0", "1.0.0")]
    [InlineData("v1.0.1", "1.0.0")]
    [InlineData("v2.0.0", "1.9.9")]
    [InlineData("1.1.0", "1.0.0")] // tag without a "v" prefix is still accepted
    public void IsNewer_ReturnsTrue_WhenLatestIsGreater(string latestTag, string currentVersion)
    {
        Assert.True(VersionComparer.IsNewer(latestTag, currentVersion));
    }

    [Theory]
    [InlineData("v1.0.0", "1.0.0")]
    [InlineData("v1.0.0", "1.1.0")]
    [InlineData("v1.0.0", "1.0.1")]
    public void IsNewer_ReturnsFalse_WhenLatestIsNotGreater(string latestTag, string currentVersion)
    {
        Assert.False(VersionComparer.IsNewer(latestTag, currentVersion));
    }

    [Fact]
    public void IsNewer_StripsBuildMetadataSuffix()
    {
        Assert.False(VersionComparer.IsNewer("v1.1.0", "1.1.0+abcdef1"));
    }

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("")]
    public void IsNewer_ReturnsFalse_ForUnparsableTag(string latestTag)
    {
        Assert.False(VersionComparer.IsNewer(latestTag, "1.0.0"));
    }
}
