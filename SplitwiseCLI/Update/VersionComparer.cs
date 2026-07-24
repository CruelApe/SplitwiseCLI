namespace SplitwiseCLI.Update;

public static class VersionComparer
{
    // GitHub tag names are "v1.1.0"; AppInfo.Version is "1.1.0" (possibly with a
    // pre-release/build suffix already stripped) - both get normalized the same way
    // so a tag can be compared directly against the running assembly's version.
    public static bool IsNewer(string latestTag, string currentVersion)
    {
        var latest = Parse(latestTag);
        var current = Parse(currentVersion);
        return latest is not null && current is not null && latest > current;
    }

    private static Version? Parse(string raw)
    {
        var trimmed = raw.TrimStart('v', 'V').Split('+')[0].Split('-')[0];
        return Version.TryParse(trimmed, out var version) ? version : null;
    }
}
