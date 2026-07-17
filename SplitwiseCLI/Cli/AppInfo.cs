using System.Reflection;

namespace SplitwiseCLI.Cli;

internal static class AppInfo
{
    public const string Author = "Tyron James L. Gono";
    public const string RepositoryUrl = "https://github.com/CruelApe/SplitwiseCLI";

    public static string Version =>
        (Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "unknown")
        .Split('+')[0]; // strip the SDK's "+<git-sha>" build metadata suffix
}
