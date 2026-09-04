using System.Globalization;
using SplitwiseCLI.Api;
using SplitwiseCLI.Import;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Services;

// Flags import rows that look like they already exist in Splitwise, so a re-run over
// overlapping data (or a bank statement covering already-entered expenses) doesn't
// silently create duplicates.
public sealed class DuplicateExpenseDetector(ISplitwiseClient client)
{
    private const int PageSize = 100;

    // Widens the query both before and after the imported date range so a candidate
    // several weeks outside it is still fetched - bounds the scan (instead of pulling
    // full history) while leaving enough room either side to recognize a handful of
    // weekly recurring cycles.
    private const int SearchWindowDays = 35;

    // A gap of any exact multiple of this many days (7, 14, 21, ...), in either
    // direction, is treated as a legitimate recurring weekly charge (e.g. a
    // subscription), not a duplicate.
    private const int RecurringIntervalDays = 7;

    // onExpensesScanned is a plain callback (not a Spectre.Console type) so this
    // service stays UI-agnostic - the CLI layer wires it up to a status spinner.
    public async Task<IReadOnlyList<Expense>> FindCandidatesAsync(
        DateTime earliest, DateTime latest, Action<int>? onExpensesScanned = null, CancellationToken cancellationToken = default)
    {
        var datedAfter = new DateTimeOffset(earliest.Date.AddDays(-SearchWindowDays));
        var datedBeforeInclusive = new DateTimeOffset(latest.Date.AddDays(SearchWindowDays));

        var matches = new List<Expense>();
        var offset = 0;
        var scanned = 0;

        while (true)
        {
            var page = await client.GetExpensesAsync(
                new ExpenseFilter(DatedAfter: datedAfter, DatedBefore: datedBeforeInclusive, Limit: PageSize, Offset: offset),
                cancellationToken);

            scanned += page.Count;
            onExpensesScanned?.Invoke(scanned);

            matches.AddRange(page.Where(e => e.Deleted != true));

            if (page.Count < PageSize)
            {
                break;
            }

            offset += PageSize;
        }

        return matches;
    }

    // Returns a human-readable reason the row looks like a duplicate of an existing
    // expense, or null if nothing matches. Description/Cost/Category/Group must all
    // match; Date then decides whether it's an exact duplicate, a possible duplicate,
    // or (when an exact multiple of a week apart, before or after) a legitimate
    // recurring charge.
    public static string? FindDuplicateReason(ValidatedExpenseRow row, IReadOnlyList<Expense> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.Equals(candidate.Description?.Trim(), row.Description, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (candidate.Category?.Id != row.CategoryId || candidate.GroupId != row.GroupId)
            {
                continue;
            }

            if (!decimal.TryParse(candidate.Cost, NumberStyles.Number, CultureInfo.InvariantCulture, out var candidateCost) ||
                candidateCost != row.Cost)
            {
                continue;
            }

            if (candidate.Date is null)
            {
                continue;
            }

            // Signed, not absolute - covers a candidate dated either before or after
            // the imported row equally (e.g. -7 and +7 both land on the same 7-day check).
            var dayDiff = (int)(candidate.Date.Value.Date - row.Date.Date).TotalDays;

            if (dayDiff == 0)
            {
                return $"Exact duplicate of expense #{candidate.Id} - same description, cost, date, category and group.";
            }

            if (dayDiff % RecurringIntervalDays != 0)
            {
                return $"Possible duplicate of expense #{candidate.Id} - same description, cost, category and group, " +
                       $"{Math.Abs(dayDiff)} day(s) apart (not an exact multiple of a week).";
            }
        }

        return null;
    }
}
