using SplitwiseCLI.Update;
using Xunit;

namespace SplitwiseCLI.Tests;

public class ChecksumFileTests
{
    [Fact]
    public void TryGetHash_FindsMatch_InStandardSha256SumFormat()
    {
        const string content =
            "37b68cec04065649839adb9ca8f84dbce1a7d33af17e1efd64c5552d86fafbb3  SplitwiseCLI-v1.1.0-win-x64.zip\n" +
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa  SHA256SUMS.txt\n";

        var found = ChecksumFile.TryGetHash(content, "SplitwiseCLI-v1.1.0-win-x64.zip", out var hash);

        Assert.True(found);
        Assert.Equal("37b68cec04065649839adb9ca8f84dbce1a7d33af17e1efd64c5552d86fafbb3", hash);
    }

    [Fact]
    public void TryGetHash_IsCaseInsensitive_ForFileName()
    {
        const string content = "37b68cec04065649839adb9ca8f84dbce1a7d33af17e1efd64c5552d86fafbb3  MyFile.zip\n";

        Assert.True(ChecksumFile.TryGetHash(content, "myfile.zip", out _));
    }

    [Fact]
    public void TryGetHash_HandlesBinaryModeMarker()
    {
        const string content = "37b68cec04065649839adb9ca8f84dbce1a7d33af17e1efd64c5552d86fafbb3 *MyFile.zip\n";

        Assert.True(ChecksumFile.TryGetHash(content, "MyFile.zip", out _));
    }

    [Fact]
    public void TryGetHash_ReturnsFalse_ForMissingFile()
    {
        const string content = "37b68cec04065649839adb9ca8f84dbce1a7d33af17e1efd64c5552d86fafbb3  SplitwiseCLI-v1.1.0-win-x64.zip\n";

        Assert.False(ChecksumFile.TryGetHash(content, "does-not-exist.zip", out var hash));
        Assert.Null(hash);
    }

    // Regression test: an actual SHA256SUMS.txt shipped by this project was a
    // captured "Get-FileHash | Format-Table" console display, not a real
    // checksums file - header row, dashes, and a hash truncated with an
    // ellipsis. This must be treated as "no usable hash", never as a match.
    [Fact]
    public void TryGetHash_ReturnsFalse_ForCapturedPowerShellTableDisplay()
    {
        const string content =
            "\n" +
            "Path                                                          Hash\n" +
            "----                                                          ----\n" +
            "D:\\src\\SplitwiseCLI\\artifacts\\SplitwiseCLI-v1.1.0-win-x64.zip 37B68CEC04065649839ADB9CA8F84DBCE1A7D33AF17E1EFD64C5552D8…\n" +
            "\n";

        Assert.False(ChecksumFile.TryGetHash(content, "SplitwiseCLI-v1.1.0-win-x64.zip", out var hash));
        Assert.Null(hash);
    }
}
