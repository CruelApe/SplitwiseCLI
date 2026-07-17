using System.Globalization;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Import;

public static class ExpenseMapper
{
    public static (CreateExpenseRequest? Request, string? Error) Map(
        ValidatedExpenseRow row,
        IReadOnlyDictionary<string, long> categoryLookup,
        IReadOnlyDictionary<string, Group> groupLookup,
        string defaultCurrency)
    {
        if (!categoryLookup.TryGetValue(row.Category, out var categoryId))
        {
            return (null, $"Unknown category '{row.Category}'.");
        }

        if (!groupLookup.TryGetValue(row.Group, out var group))
        {
            return (null, $"Unknown group '{row.Group}'.");
        }

        if (group.Members.Count == 0)
        {
            return (null, $"Group '{row.Group}' has no members to split with.");
        }

        var request = new CreateExpenseRequest
        {
            Cost = row.Cost.ToString("F2", CultureInfo.InvariantCulture),
            Description = row.Description,
            Date = row.Date.ToString("O", CultureInfo.InvariantCulture),
            CurrencyCode = defaultCurrency,
            CategoryId = categoryId,
            GroupId = group.Id,
            SplitEqually = true,
        };

        return (request, null);
    }
}
