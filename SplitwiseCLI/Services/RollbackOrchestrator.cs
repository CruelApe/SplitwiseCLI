using SplitwiseCLI.Api;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Services;

public sealed class RollbackOrchestrator(ISplitwiseClient client, ExpenseSearchService searchService)
{
    public async Task<IReadOnlyList<Expense>> FindMatchesAsync(
        string batchId, Action<int>? onExpensesScanned = null, CancellationToken cancellationToken = default)
    {
        if (!Import.BatchId.TryParseDateRange(batchId, out var start, out var end))
        {
            throw new ArgumentException(
                $"'{batchId}' is not a recognized batch id (expected format yyyyMM-yyyyMM-xxxxxx).", nameof(batchId));
        }

        return await searchService.FindTaggedExpensesAsync(start, end, batchId, onExpensesScanned, cancellationToken);
    }

    // onExpenseProcessed is a plain callback (not a Spectre.Console type) so this
    // service stays UI-agnostic - the CLI layer wires it up to a progress bar.
    public async Task<IReadOnlyList<RollbackRowResult>> DeleteAsync(
        IReadOnlyList<Expense> matches, Action<int>? onExpenseProcessed = null, CancellationToken cancellationToken = default)
    {
        var results = new List<RollbackRowResult>();

        foreach (var expense in matches)
        {
            try
            {
                await client.DeleteExpenseAsync(expense.Id, cancellationToken);
                results.Add(new RollbackRowResult(expense.Id, expense.Description, true, null));
            }
            catch (Exception ex)
            {
                // A per-expense delete failure must never abort the rest of the rollback.
                results.Add(new RollbackRowResult(expense.Id, expense.Description, false, ex.Message));
            }

            onExpenseProcessed?.Invoke(results.Count);
        }

        return results;
    }
}
