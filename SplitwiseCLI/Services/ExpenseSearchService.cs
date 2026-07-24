using SplitwiseCLI.Api;
using SplitwiseCLI.Import;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Services;

public sealed class ExpenseSearchService(ISplitwiseClient client)
{
    private const int PageSize = 100;

    // onExpensesScanned is a plain callback (not a Spectre.Console type) so this
    // service stays UI-agnostic - the CLI layer wires it up to a status spinner.
    public async Task<IReadOnlyList<Expense>> FindTaggedExpensesAsync(
        DateTimeOffset datedAfter, DateTimeOffset datedBeforeInclusive, string batchId,
        Action<int>? onExpensesScanned = null, CancellationToken cancellationToken = default)
    {
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

            matches.AddRange(page.Where(e => e.Deleted != true && BatchId.HasTag(e.Details, batchId)));

            if (page.Count < PageSize)
            {
                break;
            }

            offset += PageSize;
        }

        return matches;
    }
}
