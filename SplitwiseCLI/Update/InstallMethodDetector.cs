namespace SplitwiseCLI.Update;

public enum InstallMethod
{
    // A standalone, self-contained win-x64 build (the exact thing GitHub Releases
    // publishes) - the only install method a file-replacing self-update applies to.
    ReleaseZip,

    // Installed via 'dotnet tool install --global' - the running exe is a shim
    // under %USERPROFILE%\.dotnet\tools; its real files live elsewhere in a
    // NuGet-managed store, so replacing files here would not work.
    GlobalTool,

    // Launched via 'dotnet run'/'dotnet exec' from source - there's no standalone
    // binary at all to replace.
    RunFromSource,
}

public static class InstallMethodDetector
{
    public static InstallMethod Detect() => DetectFromProcessPath(Environment.ProcessPath);

    // Exposed separately from Environment.ProcessPath so the decision logic is testable.
    public static InstallMethod DetectFromProcessPath(string? processPath)
    {
        if (processPath is null)
        {
            return InstallMethod.ReleaseZip;
        }

        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return InstallMethod.RunFromSource;
        }

        var normalized = processPath.Replace('/', Path.DirectorySeparatorChar);
        var toolsSegment = Path.Combine(".dotnet", "tools");
        if (normalized.Contains(toolsSegment, StringComparison.OrdinalIgnoreCase))
        {
            return InstallMethod.GlobalTool;
        }

        return InstallMethod.ReleaseZip;
    }
}
