using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace SplitwiseCLI.Import;

// PowerShell/cmd do not expand "*" inside a quoted argument, so any wildcard
// resolution for paths like "C:/path/*.xlsx" has to be done by the app itself.
public static class FileResolver
{
    private static readonly char[] WildcardChars = ['*', '?'];

    public static IReadOnlyList<string> Resolve(string pathOrPattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathOrPattern);

        var normalized = pathOrPattern.Replace('/', Path.DirectorySeparatorChar);

        if (!ContainsWildcard(normalized))
        {
            if (File.Exists(normalized))
            {
                return [Path.GetFullPath(normalized)];
            }

            if (Directory.Exists(normalized))
            {
                return Directory.GetFiles(normalized, "*.xlsx", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFullPath)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return [];
        }

        var (baseDirectory, pattern) = SplitBaseAndPattern(normalized);
        if (!Directory.Exists(baseDirectory))
        {
            return [];
        }

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(pattern);
        var matchResult = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(baseDirectory)));

        return matchResult.Files
            .Select(f => Path.GetFullPath(Path.Combine(baseDirectory, f.Path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ContainsWildcard(string path) => path.IndexOfAny(WildcardChars) >= 0;

    private static (string BaseDirectory, string Pattern) SplitBaseAndPattern(string normalized)
    {
        var segments = normalized.Split(Path.DirectorySeparatorChar);
        var baseSegments = new List<string>();
        var patternSegments = new List<string>();
        var inPattern = false;

        foreach (var segment in segments)
        {
            inPattern = inPattern || ContainsWildcard(segment);
            (inPattern ? patternSegments : baseSegments).Add(segment);
        }

        var baseDirectory = string.Join(Path.DirectorySeparatorChar, baseSegments);
        if (string.IsNullOrEmpty(baseDirectory))
        {
            baseDirectory = ".";
        }
        else if (baseDirectory.EndsWith(':'))
        {
            // A bare drive letter ("C:") refers to the current directory on that drive,
            // not its root, so it must be made explicit.
            baseDirectory += Path.DirectorySeparatorChar;
        }

        var pattern = string.Join('/', patternSegments);
        return (baseDirectory, pattern);
    }
}
