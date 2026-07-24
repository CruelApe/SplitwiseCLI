using System.Text.RegularExpressions;

namespace SplitwiseCLI.Update;

// Parses the "SHA256SUMS.txt" release asset - expected to be one line per file
// in standard sha256sum format: "<hex digest>  <filename>" (two spaces, or one;
// either binary '*' or text ' ' mode marker may prefix the filename).
//
// Deliberately strict about what counts as a hash (exactly 64 hex chars) rather
// than trusting whatever token appears first on a line: some releases of this
// project have shipped a SHA256SUMS.txt that's actually a captured PowerShell
// "Get-FileHash | Format-Table" console display - header row, dashes, and a
// hash *truncated with an ellipsis* for column width, not a real digest. This
// guard makes that case reliably report "no hash found" rather than "verified"
// against a truncated value.
public static partial class ChecksumFile
{
    [GeneratedRegex("^[0-9a-fA-F]{64}$")]
    private static partial Regex FullHashPattern();

    public static bool TryGetHash(string sha256SumsContent, string fileName, out string? hash)
    {
        foreach (var rawLine in sha256SumsContent.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !FullHashPattern().IsMatch(parts[0]))
            {
                continue;
            }

            var lineFileName = parts[1].TrimStart('*');
            if (string.Equals(lineFileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                hash = parts[0];
                return true;
            }
        }

        hash = null;
        return false;
    }
}
