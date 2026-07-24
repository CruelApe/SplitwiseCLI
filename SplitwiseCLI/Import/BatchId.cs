using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace SplitwiseCLI.Import;

public static partial class BatchId
{
    public const string TagPrefix = "SPLITWISE_CLI_";

    [GeneratedRegex(@"^(\d{6})-(\d{6})-([0-9a-f]{6})$")]
    private static partial Regex FormatRegex();

    // Date-range prefix is derived from the file's own rows so the id reads as
    // human-meaningful; the suffix is random so two runs over an identical date
    // range (e.g. re-importing a corrected version of the same month) never collide.
    public static string Generate(IEnumerable<DateTime> validatedDates)
    {
        var dates = validatedDates.ToList();
        if (dates.Count == 0)
        {
            throw new ArgumentException("Cannot generate a batch id with no dates.", nameof(validatedDates));
        }

        var min = dates.Min();
        var max = dates.Max();
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant();

        return $"{min:yyyyMM}-{max:yyyyMM}-{suffix}";
    }

    public static string BuildTag(string batchId) => $"{TagPrefix}{batchId}";

    // Forces (not appends) so the tag is always the exact, unambiguous string
    // rollback matches on - a spreadsheet's pre-existing Details text is discarded
    // rather than preserved alongside it.
    public static string? ApplyTag(string? details, string? batchId) =>
        batchId is null ? details : BuildTag(batchId);

    public static bool HasTag(string? details, string batchId) =>
        details is not null && details.Contains(BuildTag(batchId), StringComparison.Ordinal);

    public static bool TryParseDateRange(string batchId, out DateTimeOffset rangeStart, out DateTimeOffset rangeEndInclusive)
    {
        rangeStart = default;
        rangeEndInclusive = default;

        var match = FormatRegex().Match(batchId);
        if (!match.Success)
        {
            return false;
        }

        if (!DateTime.TryParseExact(match.Groups[1].Value, "yyyyMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var minMonth) ||
            !DateTime.TryParseExact(match.Groups[2].Value, "yyyyMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var maxMonth))
        {
            return false;
        }

        rangeStart = new DateTimeOffset(minMonth, TimeSpan.Zero);
        rangeEndInclusive = new DateTimeOffset(maxMonth.AddMonths(1).AddTicks(-1), TimeSpan.Zero);
        return true;
    }
}
