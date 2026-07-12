using ClosedXML.Excel;
using SplitwiseCLI.Import;
using Xunit;

namespace SplitwiseCLI.Tests;

public class ExcelExpenseReaderTests : IDisposable
{
    private readonly string _filePath;

    public ExcelExpenseReaderTests()
    {
        _filePath = Path.Combine(Path.GetTempPath(), $"SplitwiseCLI.Tests-{Guid.NewGuid():N}.xlsx");

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Expenses");

        sheet.Cell(1, 1).Value = "Description";
        sheet.Cell(1, 2).Value = "Cost";
        sheet.Cell(1, 3).Value = "Date";
        sheet.Cell(1, 4).Value = "Category";
        sheet.Cell(1, 5).Value = "Group";

        sheet.Cell(2, 1).Value = "Groceries";
        sheet.Cell(2, 2).Value = 42.5;
        sheet.Cell(2, 3).Value = new DateTime(2026, 1, 15);
        sheet.Cell(2, 4).Value = "Groceries";
        sheet.Cell(2, 5).Value = "Roommates";

        sheet.Cell(3, 1).Value = "Broken row";
        sheet.Cell(3, 2).Value = "not-a-cost";
        sheet.Cell(3, 3).Value = "not-a-date";
        sheet.Cell(3, 4).Value = "Groceries";
        sheet.Cell(3, 5).Value = "Roommates";

        workbook.SaveAs(_filePath);
    }

    public void Dispose() => File.Delete(_filePath);

    [Fact]
    public void Read_ReturnsOneRowPerDataRow()
    {
        var rows = ExcelExpenseReader.Read(_filePath);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Read_ParsesWellFormedRow()
    {
        var rows = ExcelExpenseReader.Read(_filePath);
        var goodRow = rows[0];

        Assert.Equal("Groceries", goodRow.Description);
        Assert.Equal("Roommates", goodRow.Group);
        Assert.Null(goodRow.ParseError);
    }

    [Fact]
    public void Read_DoesNotThrow_ForRowWithBadCostAndDate_ValidationCatchesItInstead()
    {
        var rows = ExcelExpenseReader.Read(_filePath);
        var badRow = rows[1];

        var (validated, error) = ExpenseRowValidator.Validate(badRow);

        Assert.Null(validated);
        Assert.NotNull(error);
    }

    [Fact]
    public void Read_ThrowsClearError_WhenExpectedColumnIsMissing()
    {
        var pathWithoutHeaders = Path.Combine(Path.GetTempPath(), $"SplitwiseCLI.Tests-{Guid.NewGuid():N}.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Expenses");
            sheet.Cell(1, 1).Value = "OnlyOneColumn";
            sheet.Cell(2, 1).Value = "value";
            workbook.SaveAs(pathWithoutHeaders);
        }

        try
        {
            Assert.Throws<InvalidOperationException>(() => ExcelExpenseReader.Read(pathWithoutHeaders));
        }
        finally
        {
            File.Delete(pathWithoutHeaders);
        }
    }
}
