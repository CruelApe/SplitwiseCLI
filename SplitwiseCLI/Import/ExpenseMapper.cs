using System.Globalization;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Import;

public static class ExpenseMapper
{
    public static (CreateExpenseRequest? Request, string? Error) Map(
        ValidatedExpenseRow row,
        IReadOnlyDictionary<long, string> categoryLookup,
        IReadOnlyDictionary<long, Group> groupLookup,
        string defaultCurrency,
        string? batchId = null)
    {
        if (!categoryLookup.ContainsKey(row.CategoryId))
        {
            return (null, $"Unknown category id '{row.CategoryId}'.");
        }

        if (!groupLookup.TryGetValue(row.GroupId, out var group))
        {
            return (null, $"Unknown group id '{row.GroupId}'.");
        }

        if (group.Members.Count == 0)
        {
            return (null, $"Group '{row.GroupId}' has no members to split with.");
        }

        var request = new CreateExpenseRequest
        {
            Cost = row.Cost.ToString("F2", CultureInfo.InvariantCulture),
            Description = row.Description,
            Details = BatchId.ApplyTag(row.Details, batchId),
            Date = row.Date.ToString("O", CultureInfo.InvariantCulture),
            CurrencyCode = defaultCurrency,
            CategoryId = row.CategoryId,
            GroupId = group.Id,
            SplitEqually = true,
        };

        return (request, null);
    }
}
