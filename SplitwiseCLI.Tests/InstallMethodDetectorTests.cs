using SplitwiseCLI.Update;
using Xunit;

namespace SplitwiseCLI.Tests;

public class InstallMethodDetectorTests
{
    [Fact]
    public void DetectFromProcessPath_ReturnsRunFromSource_ForDotnetMuxer()
    {
        Assert.Equal(InstallMethod.RunFromSource, InstallMethodDetector.DetectFromProcessPath(@"C:\Program Files\dotnet\dotnet.exe"));
    }

    [Fact]
    public void DetectFromProcessPath_ReturnsGlobalTool_ForToolsShimPath()
    {
        Assert.Equal(
            InstallMethod.GlobalTool,
            InstallMethodDetector.DetectFromProcessPath(@"C:\Users\tyron\.dotnet\tools\splitwise.exe"));
    }

    [Fact]
    public void DetectFromProcessPath_ReturnsReleaseZip_ForStandaloneExe()
    {
        Assert.Equal(
            InstallMethod.ReleaseZip,
            InstallMethodDetector.DetectFromProcessPath(@"D:\Tools\SplitwiseCLI\splitwise.exe"));
    }

    [Fact]
    public void DetectFromProcessPath_ReturnsReleaseZip_WhenPathIsNull()
    {
        Assert.Equal(InstallMethod.ReleaseZip, InstallMethodDetector.DetectFromProcessPath(null));
    }
}
