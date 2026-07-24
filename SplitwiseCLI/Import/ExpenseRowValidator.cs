using System.Globalization;

namespace SplitwiseCLI.Import;

public sealed class ValidatedExpenseRow
{
    public required string Description { get; init; }
    public string? Details { get; init; }
    public required decimal Cost { get; init; }
    public required DateTime Date { get; init; }
    public required long CategoryId { get; init; }
    public required long GroupId { get; init; }
}

public static class ExpenseRowValidator
{
    private const NumberStyles IdNumberStyles = NumberStyles.Integer | NumberStyles.AllowThousands;

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

        if (string.IsNullOrWhiteSpace(row.RawCategory))
        {
            return (null, "Category is required.");
        }

        if (string.IsNullOrWhiteSpace(row.RawGroup))
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

        if (!long.TryParse(row.RawCategory, IdNumberStyles, CultureInfo.InvariantCulture, out var categoryId))
        {
            return (null, $"Category '{row.RawCategory}' is not a valid numeric category id.");
        }

        if (!long.TryParse(row.RawGroup, IdNumberStyles, CultureInfo.InvariantCulture, out var groupId))
        {
            return (null, $"Group '{row.RawGroup}' is not a valid numeric group id.");
        }

        return (new ValidatedExpenseRow
        {
            Description = row.Description.Trim(),
            Details = string.IsNullOrWhiteSpace(row.Details) ? null : row.Details.Trim(),
            Cost = cost,
            Date = date,
            CategoryId = categoryId,
            GroupId = groupId,
        }, null);
    }
}
