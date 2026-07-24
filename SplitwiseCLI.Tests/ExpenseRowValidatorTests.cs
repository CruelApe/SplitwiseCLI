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
        RawCategory = "101",
        RawGroup = "55",
    };

    [Fact]
    public void Validate_AcceptsWellFormedRow()
    {
        var (row, error) = ExpenseRowValidator.Validate(ValidRow());

        Assert.Null(error);
        Assert.NotNull(row);
        Assert.Equal("Groceries", row!.Description);
        Assert.Equal(42.50m, row.Cost);
        Assert.Equal(101, row.CategoryId);
        Assert.Equal(55, row.GroupId);
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
            "Description" => new ExpenseRow { SourceFile = row.SourceFile, RowNumber = row.RowNumber, Description = " ", RawCost = row.RawCost, RawDate = row.RawDate, RawCategory = row.RawCategory, RawGroup = row.RawGroup },
            "Category" => new ExpenseRow { SourceFile = row.SourceFile, RowNumber = row.RowNumber, Description = row.Description, RawCost = row.RawCost, RawDate = row.RawDate, RawCategory = " ", RawGroup = row.RawGroup },
            "Group" => new ExpenseRow { SourceFile = row.SourceFile, RowNumber = row.RowNumber, Description = row.Description, RawCost = row.RawCost, RawDate = row.RawDate, RawCategory = row.RawCategory, RawGroup = " " },
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
        row = new ExpenseRow { SourceFile = row.SourceFile, RowNumber = row.RowNumber, Description = row.Description, RawCost = rawCost, RawDate = row.RawDate, RawCategory = row.RawCategory, RawGroup = row.RawGroup };

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
        row = new ExpenseRow { SourceFile = row.SourceFile, RowNumber = row.RowNumber, Description = row.Description, RawCost = row.RawCost, RawDate = rawDate, RawCategory = row.RawCategory, RawGroup = row.RawGroup };

        var (validated, error) = ExpenseRowValidator.Validate(row);

        Assert.Null(validated);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("Groceries")]
    [InlineData("12.5")]
    [InlineData("")]
    public void Validate_RejectsNonNumericCategory(string rawCategory)
    {
        var row = ValidRow();
        row = new ExpenseRow { SourceFile = row.SourceFile, RowNumber = row.RowNumber, Description = row.Description, RawCost = row.RawCost, RawDate = row.RawDate, RawCategory = rawCategory, RawGroup = row.RawGroup };

        var (validated, error) = ExpenseRowValidator.Validate(row);

        Assert.Null(validated);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("Roommates")]
    [InlineData("55.5")]
    public void Validate_RejectsNonNumericGroup(string rawGroup)
    {
        var row = ValidRow();
        row = new ExpenseRow { SourceFile = row.SourceFile, RowNumber = row.RowNumber, Description = row.Description, RawCost = row.RawCost, RawDate = row.RawDate, RawCategory = row.RawCategory, RawGroup = rawGroup };

        var (validated, error) = ExpenseRowValidator.Validate(row);

        Assert.Null(validated);
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_AllowsMissingDetails()
    {
        var (validated, error) = ExpenseRowValidator.Validate(ValidRow());

        Assert.Null(error);
        Assert.Null(validated!.Details);
    }

    [Fact]
    public void Validate_PassesThroughDetails_WhenProvided()
    {
        var row = ValidRow();
        row = new ExpenseRow { SourceFile = row.SourceFile, RowNumber = row.RowNumber, Description = row.Description, RawCost = row.RawCost, RawDate = row.RawDate, RawCategory = row.RawCategory, RawGroup = row.RawGroup, Details = "  Weekly shop  " };

        var (validated, error) = ExpenseRowValidator.Validate(row);

        Assert.Null(error);
        Assert.Equal("Weekly shop", validated!.Details);
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
