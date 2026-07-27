using ClosedXML.Excel;
using SplitwiseCLI.Api;
using SplitwiseCLI.Configuration;
using SplitwiseCLI.Models;
using SplitwiseCLI.Services;
using SplitwiseCLI.Statements;
using Xunit;

namespace SplitwiseCLI.Tests;

public class MergeOrchestratorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"SplitwiseCLI.Tests-{Guid.NewGuid():N}");

    public MergeOrchestratorTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string WriteWorkbook(params (string Description, string Cost, string Date, string Category, string Group, string? Details)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Expenses");
        string[] headers = ["Description", "Cost", "Date", "Category", "Group", "Details"];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        for (var r = 0; r < rows.Length; r++)
        {
            sheet.Cell(r + 2, 1).Value = rows[r].Description;
            sheet.Cell(r + 2, 2).Value = rows[r].Cost;
            sheet.Cell(r + 2, 3).Value = rows[r].Date;
            sheet.Cell(r + 2, 4).Value = rows[r].Category;
            sheet.Cell(r + 2, 5).Value = rows[r].Group;
            sheet.Cell(r + 2, 6).Value = rows[r].Details ?? "";
        }

        var path = Path.Combine(_root, $"{Guid.NewGuid():N}.xlsx");
        workbook.SaveAs(path);
        return path;
    }

    private static MergeOrchestrator CreateOrchestrator(ISplitwiseClient client, IPdfTextExtractor? pdfTextExtractor = null) =>
        new(client, new CategoryLookupService(client), new GroupLookupService(client),
            new AppConfig("key", "https://example.invalid", null), pdfTextExtractor ?? new FakeTextExtractor(""));

    [Fact]
    public async Task PrepareAsync_CombinesValidRows_AcrossMultipleFiles_InFileOrder()
    {
        var file1 = WriteWorkbook(("Groceries", "10.00", "2026-05-01", "101", "55", null));
        var file2 = WriteWorkbook(("Rent", "500.00", "2026-06-01", "101", "55", "Notes"));

        var plan = await CreateOrchestrator(new FakeSplitwiseClient()).PrepareAsync([file1, file2]);

        Assert.Equal(2, plan.Rows.Count);
        Assert.Empty(plan.Issues);
        Assert.Equal("Groceries", plan.Rows[0].Description);
        Assert.Equal("Rent", plan.Rows[1].Description);
        Assert.Equal("Notes", plan.Rows[1].Details);
    }

    [Fact]
    public async Task PrepareAsync_PutsInvalidRows_InIssues_NotRows()
    {
        var file = WriteWorkbook(
            ("Good expense", "10.00", "2026-01-01", "101", "55", null),
            ("Bad cost", "not-a-number", "2026-01-02", "101", "55", null));

        var plan = await CreateOrchestrator(new FakeSplitwiseClient()).PrepareAsync([file]);

        Assert.Single(plan.Rows);
        var issue = Assert.Single(plan.Issues);
        Assert.Equal("Bad cost", issue.Description);
        Assert.Contains("not-a-number", issue.Error);
    }

    [Fact]
    public async Task PrepareAsync_DoesNotRequireCategoryOrGroup_ToExistOnTheAccount()
    {
        // Merge doesn't create expenses, so a row referencing an id the account
        // doesn't currently have is still merged through, unlike import.
        var file = WriteWorkbook(("Unknown ids", "10.00", "2026-01-01", "999999", "888888", null));

        var plan = await CreateOrchestrator(new FakeSplitwiseClient()).PrepareAsync([file]);

        var row = Assert.Single(plan.Rows);
        Assert.Equal(999999, row.CategoryId);
        Assert.Equal(888888, row.GroupId);
        Assert.Empty(plan.Issues);
    }

    [Fact]
    public async Task PrepareAsync_NeverCallsCreateOrDeleteExpense()
    {
        var file = WriteWorkbook(("Good expense", "10.00", "2026-01-01", "101", "55", null));
        var client = new FakeSplitwiseClient();

        await CreateOrchestrator(client).PrepareAsync([file]);

        Assert.False(client.CreateOrDeleteCalled);
    }

    [Fact]
    public async Task PrepareAsync_LoadsFullLiveCategoryAndGroupLists_ExcludingPseudoGroupZero()
    {
        var file = WriteWorkbook(("Good expense", "10.00", "2026-01-01", "101", "55", null));

        var plan = await CreateOrchestrator(new FakeSplitwiseClient()).PrepareAsync([file]);

        Assert.Single(plan.Categories);
        Assert.Equal("Food", plan.Categories[0].Name);
        Assert.Single(plan.Categories[0].Subcategories);
        Assert.DoesNotContain(plan.Groups, g => g.Id == 0);
        Assert.Contains(plan.Groups, g => g.Id == 55);
    }

    [Fact]
    public void Write_ProducesExpectedSheetsHeadersAndFormulas()
    {
        var plan = new MergePlan(
            [new MergedExpenseRow("file.xlsx", "Groceries", 10.00m, new DateTime(2026, 5, 1), 101, 55, "Notes")],
            [],
            [new Category { Id = 1, Name = "Food", Subcategories = [new Subcategory { Id = 101, Name = "Groceries" }] }],
            [new Group { Id = 55, Name = "Roommates", Members = [new GroupMember { Id = 1 }, new GroupMember { Id = 2 }] }],
            "USD");

        var outputPath = Path.Combine(_root, "merged.xlsx");
        CreateOrchestrator(new FakeSplitwiseClient()).Write(plan, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var sheetNames = workbook.Worksheets.Select(w => w.Name).ToList();
        Assert.Equal(["Expenses", "Category Reference Data", "Group Reference Data"], sheetNames);

        var expenses = workbook.Worksheet("Expenses");
        Assert.Equal("Details", expenses.Cell(1, 10).GetString());
        Assert.Equal(10.00, expenses.Cell(2, 1).GetDouble());
        Assert.Equal("2026-05-01", expenses.Cell(2, 3).GetString());
        Assert.Equal("USD", expenses.Cell(2, 4).GetString());
        Assert.Contains("XLOOKUP(H2,'Category Reference Data'!C:C,'Category Reference Data'!B:B", expenses.Cell(2, 5).FormulaA1);
        Assert.Contains("XLOOKUP(I2,'Group Reference Data'!A:A,'Group Reference Data'!B:B", expenses.Cell(2, 6).FormulaA1);
        Assert.Equal("true", expenses.Cell(2, 7).GetString());
        Assert.Equal(101, expenses.Cell(2, 8).GetValue<long>());
        Assert.Equal(55, expenses.Cell(2, 9).GetValue<long>());
        Assert.Equal("Notes", expenses.Cell(2, 10).GetString());

        var categories = workbook.Worksheet("Category Reference Data");
        Assert.Equal("Food", categories.Cell(2, 1).GetString());
        Assert.Equal("Groceries", categories.Cell(2, 2).GetString());
        Assert.Equal(101, categories.Cell(2, 3).GetValue<long>());

        var groups = workbook.Worksheet("Group Reference Data");
        Assert.Equal(55, groups.Cell(2, 1).GetValue<long>());
        Assert.Equal("Roommates", groups.Cell(2, 2).GetString());
        Assert.Equal(2, groups.Cell(2, 3).GetValue<int>());
    }

    [Fact]
    public void Write_AddsListDataValidation_ForCategoryAndGroupColumns()
    {
        var plan = new MergePlan(
            [new MergedExpenseRow("file.xlsx", "Groceries", 10.00m, new DateTime(2026, 5, 1), 101, 55, null)],
            [],
            [new Category { Id = 1, Name = "Food", Subcategories = [new Subcategory { Id = 101, Name = "Groceries" }] }],
            [new Group { Id = 55, Name = "Roommates", Members = [new GroupMember { Id = 1 }, new GroupMember { Id = 2 }] }],
            "USD");

        var outputPath = Path.Combine(_root, "merged-validation.xlsx");
        CreateOrchestrator(new FakeSplitwiseClient()).Write(plan, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var expenses = workbook.Worksheet("Expenses");

        var categoryValidation = Assert.Single(expenses.DataValidations, v => v.Ranges.Any(r => r.RangeAddress.ToString() == "H2:H2"));
        Assert.Equal(XLAllowedValues.List, categoryValidation.AllowedValues);
        Assert.Equal("='Category Reference Data'!$C$2:$C$2", categoryValidation.Value);

        var groupValidation = Assert.Single(expenses.DataValidations, v => v.Ranges.Any(r => r.RangeAddress.ToString() == "I2:I2"));
        Assert.Equal(XLAllowedValues.List, groupValidation.AllowedValues);
        Assert.Equal("='Group Reference Data'!$A$2:$A$2", groupValidation.Value);
    }

    [Theory]
    [InlineData("2026-05-01", "2026-05-20", "Expenses_May.xlsx")]
    [InlineData("2026-05-01", "2026-07-15", "Expenses_May-July.xlsx")]
    public void BuildDefaultOutputFileName_UsesMonthNames_CollapsingWhenSameMonth(string minDate, string maxDate, string expected)
    {
        var rows = new List<MergedExpenseRow>
        {
            new("a.xlsx", "A", 1m, DateTime.Parse(minDate), 1, 1, null),
            new("a.xlsx", "B", 1m, DateTime.Parse(maxDate), 1, 1, null),
        };

        Assert.Equal(expected, MergeOrchestrator.BuildDefaultOutputFileName(rows));
    }

    [Fact]
    public void DetermineDefaultOutputDirectory_ReturnsSharedFolder_WhenAllFilesAreInIt()
    {
        var file1 = Path.Combine(_root, "jan.xlsx");
        var file2 = Path.Combine(_root, "feb.xlsx");

        var directory = MergeOrchestrator.DetermineDefaultOutputDirectory([file1, file2]);

        Assert.Equal(Path.GetFullPath(_root), Path.GetFullPath(directory));
    }

    [Fact]
    public void DetermineDefaultOutputDirectory_ReturnsMergedFilesFolder_WhenFilesAreInDifferentFolders()
    {
        var subA = Path.Combine(_root, "a");
        var subB = Path.Combine(_root, "b");
        Directory.CreateDirectory(subA);
        Directory.CreateDirectory(subB);
        var file1 = Path.Combine(subA, "jan.xlsx");
        var file2 = Path.Combine(subB, "feb.xlsx");

        var directory = MergeOrchestrator.DetermineDefaultOutputDirectory([file1, file2]);

        Assert.Equal(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Merged Files")), Path.GetFullPath(directory));
    }

    [Fact]
    public async Task PrepareAsync_ParsesPdfStatement_WithBlankCategoryAndGroup()
    {
        const string ColesText = """
            Processed Date  Transaction Date  Details                          Amount
            01/05/26        01/05/26          Coles Supermarket Greenvale       $45.67 Dr
            """;

        var plan = await CreateOrchestrator(new FakeSplitwiseClient(), new FakeTextExtractor(ColesText))
            .PrepareAsync(["statement.pdf"]);

        var row = Assert.Single(plan.Rows);
        Assert.Equal("Coles Supermarket Greenvale", row.Description);
        Assert.Equal(45.67m, row.Cost);
        Assert.Null(row.CategoryId);
        Assert.Null(row.GroupId);
        Assert.Empty(plan.Issues);
    }

    [Fact]
    public async Task PrepareAsync_ReportsIssue_ForUnrecognizedStatementFormat()
    {
        var plan = await CreateOrchestrator(new FakeSplitwiseClient(), new FakeTextExtractor("not a real statement"))
            .PrepareAsync(["statement.pdf"]);

        Assert.Empty(plan.Rows);
        var issue = Assert.Single(plan.Issues);
        Assert.Contains("Unrecognized statement format", issue.Error);
    }

    [Fact]
    public async Task PrepareAsync_ReportsIssue_ForUnsupportedFileType()
    {
        var plan = await CreateOrchestrator(new FakeSplitwiseClient()).PrepareAsync(["notes.txt"]);

        Assert.Empty(plan.Rows);
        var issue = Assert.Single(plan.Issues);
        Assert.Contains("Unsupported file type", issue.Error);
    }

    [Fact]
    public void Write_LeavesCategoryAndGroupCellsBlank_WhenRowIdsAreNull()
    {
        var plan = new MergePlan(
            [new MergedExpenseRow("statement.pdf", "Coles Supermarket", 45.67m, new DateTime(2026, 5, 1), null, null, null)],
            [],
            [],
            [],
            "AUD");

        var outputPath = Path.Combine(_root, "merged-blank.xlsx");
        CreateOrchestrator(new FakeSplitwiseClient()).Write(plan, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var expenses = workbook.Worksheet("Expenses");
        Assert.True(expenses.Cell(2, 8).IsEmpty());
        Assert.True(expenses.Cell(2, 9).IsEmpty());
    }

    private sealed class FakeTextExtractor(string text) : IPdfTextExtractor
    {
        public string ExtractText(string filePath) => text;
    }

    private sealed class FakeSplitwiseClient : ISplitwiseClient
    {
        public bool CreateOrDeleteCalled { get; private set; }

        public Task<User> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new User { Id = 1, DefaultCurrency = "USD" });

        public Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Category>
            {
                new() { Id = 1, Name = "Food", Subcategories = [new Subcategory { Id = 101, Name = "Groceries" }] },
            });

        public Task<List<Group>> GetGroupsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Group>
            {
                new() { Id = 0, Name = "Non-group expenses", Members = [] },
                new() { Id = 55, Name = "Roommates", Members = [new GroupMember { Id = 1 }, new GroupMember { Id = 2 }] },
            });

        public Task<User> GetUserAsync(long id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<Group> GetGroupAsync(long id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<List<Friend>> GetFriendsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<Friend> GetFriendAsync(long id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<List<Expense>> GetExpensesAsync(ExpenseFilter filter, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<Expense> GetExpenseAsync(long id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<List<Comment>> GetCommentsAsync(long expenseId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<List<Notification>> GetNotificationsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<List<Currency>> GetCurrenciesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<CreateExpenseResponse> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken = default)
        {
            CreateOrDeleteCalled = true;
            return Task.FromResult(new CreateExpenseResponse { Success = true, Expenses = [new CreatedExpense { Id = 1 }] });
        }

        public Task DeleteExpenseAsync(long id, CancellationToken cancellationToken = default)
        {
            CreateOrDeleteCalled = true;
            return Task.CompletedTask;
        }
    }
}
