using System.Globalization;

namespace SplitwiseCLI.Import;

public sealed class ValidatedExpenseRow
{
    public required string Description { get; init; }
    public required decimal Cost { get; init; }
    public required DateTime Date { get; init; }
    public required string Category { get; init; }
    public required string Group { get; init; }
}

public static class ExpenseRowValidator
{
    public static (ValidatedExpenseRow? Row, string? Error) Validate(ExpenseRow row)
    {
        if (row.ParseError is not null)
        {
            return (null, row.ParseError);
        }

        if (string.IsNullOrWhiteSpace(row.Description))
        {
            return (null, "Description is required.");
        }

        if (string.IsNullOrWhiteSpace(row.Category))
        {
            return (null, "Category is required.");
        }

        if (string.IsNullOrWhiteSpace(row.Group))
        {
            return (null, "Group is required.");
        }

        if (!decimal.TryParse(row.RawCost, NumberStyles.Number, CultureInfo.InvariantCulture, out var cost))
        {
            return (null, $"Cost '{row.RawCost}' is not a valid number.");
        }

        if (cost <= 0)
        {
            return (null, $"Cost must be greater than zero (got {cost}).");
        }

        if (!DateTime.TryParse(row.RawDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return (null, $"Date '{row.RawDate}' is not a valid date.");
        }

        return (new ValidatedExpenseRow
        {
            Description = row.Description.Trim(),
            Cost = cost,
            Date = date,
            Category = row.Category.Trim(),
            Group = row.Group.Trim(),
        }, null);
    }
}
