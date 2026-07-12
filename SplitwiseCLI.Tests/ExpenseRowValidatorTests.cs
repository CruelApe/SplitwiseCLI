using SplitwiseCLI.Import;
using Xunit;

namespace SplitwiseCLI.Tests;

public class ExpenseRowValidatorTests
{
    private static ExpenseRow ValidRow() => new()
    {
        SourceFile = "test.xlsx",
        RowNumber = 2,
        Description = "Groceries",
        RawCost = "42.50",
        RawDate = "2026-01-15",
        Category = "Groceries",
        Group = "Roommates",
    };

    [Fact]
    public void Validate_AcceptsWellFormedRow()
    {
        var (row, error) = ExpenseRowValidator.Validate(ValidRow());

        Assert.Null(error);
        Assert.NotNull(row);
        Assert.Equal("Groceries", row!.Description);
        Assert.Equal(42.50m, row.Cost);
    }

    [Theory]
    [InlineData("Description")]
    [InlineData("Category")]
    [InlineData("Group")]
    public void Validate_RejectsMissingRequiredField(string field)
    {
        var row = ValidRow();
        row = field switch
        {
            "Description" => new ExpenseRow { SourceFile = row.SourceFile, RowNumber = row.RowNumber, Description = " ", RawCost = row.RawCost, RawDate = row.RawDate, Category = row.Category, Group = row.Group },
            "Category" => new ExpenseRow { SourceFile = row.SourceFile, RowNumber = row.RowNumber, Description = row.Description, RawCost = row.RawCost, RawDate = row.RawDate, Category = " ", Group = row.Group },
            "Group" => new ExpenseRow { SourceFile = row.SourceFile, RowNumber = row.RowNumber, Description = row.Description, RawCost = row.RawCost, RawDate = row.RawDate, Category = row.Category, Group = " " },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        var (validated, error) = ExpenseRowValidator.Validate(row);

        Assert.Null(validated);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData(null)]
    public void Validate_RejectsInvalidCost(string? rawCost)
    {
        var row = ValidRow();
        row = new ExpenseRow { SourceFile = row.SourceFile, RowNumber = row.RowNumber, Description = row.Description, RawCost = rawCost, RawDate = row.RawDate, Category = row.Category, Group = row.Group };

        var (validated, error) = ExpenseRowValidator.Validate(row);

        Assert.Null(validated);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData(null)]
    public void Validate_RejectsInvalidDate(string? rawDate)
    {
        var row = ValidRow();
        row = new ExpenseRow { SourceFile = row.SourceFile, RowNumber = row.RowNumber, Description = row.Description, RawCost = row.RawCost, RawDate = rawDate, Category = row.Category, Group = row.Group };

        var (validated, error) = ExpenseRowValidator.Validate(row);

        Assert.Null(validated);
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_PropagatesReaderParseError()
    {
        var row = new ExpenseRow { SourceFile = "test.xlsx", RowNumber = 3, ParseError = "boom" };

        var (validated, error) = ExpenseRowValidator.Validate(row);

        Assert.Null(validated);
        Assert.Equal("boom", error);
    }
}
