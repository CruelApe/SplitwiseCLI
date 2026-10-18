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
        Assert.Equal(["Expenses", "Non-Expense", "Category Reference Data", "Group Reference Data"], sheetNames);

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
    public async Task PrepareAsync_ReportsStatementParserIssues_AlongsideTheRowsItCouldRead()
    {
        const string LatitudeText = """
            Date Card Description Debits Credits
            04/07/2026 1234 Anthropic* Claude Sub San Francisco Ca
            05/07/2026 1234 Kmart 1262 Broadmeadows Vic $114.45
            """;

        var plan = await CreateOrchestrator(new FakeSplitwiseClient(), new FakeTextExtractor(LatitudeText))
            .PrepareAsync(["statement.pdf"]);

        var row = Assert.Single(plan.Rows);
        Assert.Equal("Kmart 1262 Broadmeadows Vic", row.Description);
        var issue = Assert.Single(plan.Issues);
        Assert.Equal("statement.pdf", issue.SourceFile);
        Assert.Equal("Anthropic* Claude Sub San Francisco Ca", issue.Description);
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

    // Account shaped like the reference workbook's, so the built-in rules resolve to real ids.
    private static FakeSplitwiseClient ClientWithReferenceAccount() => new(
        [
            new() { Id = 3, Name = "Food and drink", Subcategories = [new Subcategory { Id = 12, Name = "Groceries" }, new Subcategory { Id = 13, Name = "Dining out" }] },
        ],
        [
            new() { Id = 0, Name = "Non-group expenses", Members = [] },
            new() { Id = 51170136, Name = "🛒 Groceries + Shopping", Members = [new GroupMember { Id = 1 }, new GroupMember { Id = 2 }] },
        ]);

    private const string ClassifiablePdfText = """
        Processed Date  Transaction Date  Details                              Amount
        01/05/26        01/05/26          Coles 0608 Greenvale Vic              $45.67 Dr
        02/05/26        02/05/26          Google Microsoft 365 Barangaroo Aus   $16.00 Dr
        03/05/26        03/05/26          Some Merchant The Rules Never Saw     $9.99 Dr
        """;

    [Fact]
    public async Task PrepareAsync_PreAssignsCategoryAndGroup_ForPdfRowsMatchingARule()
    {
        var plan = await CreateOrchestrator(ClientWithReferenceAccount(), new FakeTextExtractor(ClassifiablePdfText))
            .PrepareAsync(["statement.pdf"]);

        var coles = Assert.Single(plan.Rows, r => r.Description.StartsWith("Coles"));
        Assert.Equal(12, coles.CategoryId);
        Assert.Equal(51170136, coles.GroupId);
    }

    [Fact]
    public async Task PrepareAsync_MovesNonExpensePdfRows_ToNonExpenseRows_WithDiningOutAndGroupZero()
    {
        var plan = await CreateOrchestrator(ClientWithReferenceAccount(), new FakeTextExtractor(ClassifiablePdfText))
            .PrepareAsync(["statement.pdf"]);

        var google = Assert.Single(plan.NonExpenseRows);
        Assert.Equal("Google Microsoft 365 Barangaroo Aus", google.Description);
        Assert.Equal(16.00m, google.Cost);
        Assert.Equal(13, google.CategoryId);
        Assert.Equal(0, google.GroupId);
        Assert.DoesNotContain(plan.Rows, r => r.Description.StartsWith("Google"));
    }

    [Fact]
    public async Task PrepareAsync_LeavesUnmatchedPdfRowsBlank_InExpenses()
    {
        var plan = await CreateOrchestrator(ClientWithReferenceAccount(), new FakeTextExtractor(ClassifiablePdfText))
            .PrepareAsync(["statement.pdf"]);

        var unknown = Assert.Single(plan.Rows, r => r.Description.StartsWith("Some Merchant"));
        Assert.Null(unknown.CategoryId);
        Assert.Null(unknown.GroupId);
        Assert.Equal(2, plan.Rows.Count);
        Assert.Empty(plan.Issues);
    }

    [Fact]
    public async Task PrepareAsync_KeepsRowsInSourceOrder_WithinEachSheet()
    {
        const string text = """
            Processed Date  Transaction Date  Details                              Amount
            03/05/26        03/05/26          Google Youtube Bara                   $16.99 Dr
            01/05/26        01/05/26          Telstra Services                      $129.52 Dr
            02/05/26        02/05/26          Coles 0608 Greenvale Vic              $45.67 Dr
            """;

        var plan = await CreateOrchestrator(ClientWithReferenceAccount(), new FakeTextExtractor(text))
            .PrepareAsync(["statement.pdf"]);

        Assert.Equal(["Google Youtube Bara", "Telstra Services"], plan.NonExpenseRows.Select(r => r.Description));
    }

    [Fact]
    public async Task PrepareAsync_DoesNotReclassifyXlsxRows_TheyKeepTheirOwnCategoryAndGroup()
    {
        // "Google" would be non-expense for a PDF row, but an xlsx row already carries
        // the Category/Group the user chose, and must come through untouched.
        var file = WriteWorkbook(("Google Microsoft 365", "16.00", "2026-05-01", "101", "55", null));

        var plan = await CreateOrchestrator(ClientWithReferenceAccount()).PrepareAsync([file]);

        var row = Assert.Single(plan.Rows);
        Assert.Equal(101, row.CategoryId);
        Assert.Equal(55, row.GroupId);
        Assert.Empty(plan.NonExpenseRows);
    }

    [Fact]
    public void MergePlan_AllRows_ListsExpenseRowsThenNonExpenseRows()
    {
        var expense = new MergedExpenseRow("a.pdf", "Coles", 1m, new DateTime(2026, 5, 1), 12, 1, null);
        var nonExpense = new MergedExpenseRow("a.pdf", "Google", 2m, new DateTime(2026, 5, 2), 13, 0, null);

        var plan = new MergePlan([expense], [], [], [], "AUD") { NonExpenseRows = [nonExpense] };

        Assert.Equal([expense, nonExpense], plan.AllRows);
    }

    [Fact]
    public void Write_ProducesNonExpenseSheet_InTheReferenceColumnOrderAndFormat()
    {
        var plan = new MergePlan([], [], [], [], "AUD")
        {
            NonExpenseRows =
            [
                new MergedExpenseRow("a.pdf", "Google Microsoft 365 Barangaroo Aus", 16m, new DateTime(2026, 7, 15), 13, 0, null),
                new MergedExpenseRow("a.pdf", "Merry Rose Seastre D 2181859225 Asawa", 1874m, new DateTime(2026, 7, 16), 13, 0, null),
            ],
        };

        var outputPath = Path.Combine(_root, "merged-nonexpense.xlsx");
        CreateOrchestrator(new FakeSplitwiseClient()).Write(plan, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var sheet = workbook.Worksheet("Non-Expense");

        string[] expectedHeaders =
            ["Cost", "Description", "Details", "Date", "Currency Code", "Category Id", "Group Id", "Split Equally", "Group"];
        Assert.Equal(expectedHeaders, Enumerable.Range(1, 9).Select(c => sheet.Cell(1, c).GetString()));

        Assert.Equal(16.00, sheet.Cell(2, 1).GetDouble());
        Assert.Equal("Google Microsoft 365 Barangaroo Aus", sheet.Cell(2, 2).GetString());
        Assert.Equal("SPLITWISE_CLI", sheet.Cell(2, 3).GetString());
        Assert.Equal("2026-07-15", sheet.Cell(2, 4).GetString());
        Assert.Equal("AUD", sheet.Cell(2, 5).GetString());
        Assert.Equal(13, sheet.Cell(2, 6).GetValue<long>());
        Assert.Equal(0, sheet.Cell(2, 7).GetValue<long>());
        Assert.Equal("true", sheet.Cell(2, 8).GetString());
        Assert.Equal("Non-group expenses", sheet.Cell(2, 9).GetString());
        Assert.Equal(1874.00, sheet.Cell(3, 1).GetDouble());
        Assert.Equal("Merry Rose Seastre D 2181859225 Asawa", sheet.Cell(3, 2).GetString());
    }

    [Fact]
    public void Write_StylesNonExpenseSheet_WithTheReferenceHeaderBandingAndWidths()
    {
        var plan = new MergePlan([], [], [], [], "AUD")
        {
            NonExpenseRows =
            [
                new MergedExpenseRow("a.pdf", "One", 1m, new DateTime(2026, 7, 1), 13, 0, null),
                new MergedExpenseRow("a.pdf", "Two", 2m, new DateTime(2026, 7, 2), 13, 0, null),
            ],
        };

        var outputPath = Path.Combine(_root, "merged-nonexpense-style.xlsx");
        CreateOrchestrator(new FakeSplitwiseClient()).Write(plan, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var sheet = workbook.Worksheet("Non-Expense");

        var header = sheet.Cell(1, 1).Style;
        Assert.True(header.Font.Bold);
        Assert.Equal(XLColor.White, header.Font.FontColor);
        Assert.Equal(XLColor.FromHtml("#1B365D"), header.Fill.BackgroundColor);
        Assert.Equal(XLAlignmentHorizontalValues.Center, header.Alignment.Horizontal);

        Assert.Equal("\\$#,##0.00", sheet.Cell(2, 1).Style.NumberFormat.Format);
        Assert.Equal(XLAlignmentHorizontalValues.Left, sheet.Cell(2, 2).Style.Alignment.Horizontal);
        Assert.Equal(XLAlignmentHorizontalValues.Center, sheet.Cell(2, 5).Style.Alignment.Horizontal);

        // Every other data row is banded, starting with the first.
        Assert.Equal(XLColor.FromHtml("#F7FAFC"), sheet.Cell(2, 2).Style.Fill.BackgroundColor);
        Assert.NotEqual(XLColor.FromHtml("#F7FAFC"), sheet.Cell(3, 2).Style.Fill.BackgroundColor);
        Assert.Equal(XLBorderStyleValues.Thin, sheet.Cell(2, 2).Style.Border.BottomBorder);

        Assert.Equal(49.57, sheet.Column(2).Width, 2);
        Assert.Equal(19.71, sheet.Column(9).Width, 2);
    }

    [Fact]
    public void Write_AlwaysWritesNonExpenseSheet_HeaderOnlyWhenThereAreNoNonExpenseRows()
    {
        var plan = new MergePlan([], [], [], [], "AUD");

        var outputPath = Path.Combine(_root, "merged-no-nonexpense.xlsx");
        CreateOrchestrator(new FakeSplitwiseClient()).Write(plan, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var sheet = workbook.Worksheet("Non-Expense");
        Assert.Equal("Cost", sheet.Cell(1, 1).GetString());
        Assert.Equal("Group", sheet.Cell(1, 9).GetString());
        Assert.True(sheet.Cell(2, 1).IsEmpty());
    }

    [Fact]
    public void Write_DoesNotAddNonExpenseRows_ToTheExpensesSheet()
    {
        var plan = new MergePlan(
            [new MergedExpenseRow("a.pdf", "Coles", 10m, new DateTime(2026, 5, 1), 12, 1, null)], [], [], [], "AUD")
        {
            NonExpenseRows = [new MergedExpenseRow("a.pdf", "Google", 2m, new DateTime(2026, 5, 2), 13, 0, null)],
        };

        var outputPath = Path.Combine(_root, "merged-separate.xlsx");
        CreateOrchestrator(new FakeSplitwiseClient()).Write(plan, outputPath);

        using var workbook = new XLWorkbook(outputPath);
        var expenses = workbook.Worksheet("Expenses");
        Assert.Equal("Coles", expenses.Cell(2, 2).GetString());
        Assert.True(expenses.Cell(3, 2).IsEmpty());
    }

    private sealed class FakeTextExtractor(string text) : IPdfTextExtractor
    {
        public string ExtractText(string filePath) => text;
    }

    private sealed class FakeSplitwiseClient(List<Category>? categories = null, List<Group>? groups = null) : ISplitwiseClient
    {
        public bool CreateOrDeleteCalled { get; private set; }

        public Task<User> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new User { Id = 1, DefaultCurrency = "USD" });

        public Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(categories ?? new List<Category>
            {
                new() { Id = 1, Name = "Food", Subcategories = [new Subcategory { Id = 101, Name = "Groceries" }] },
            });

        public Task<List<Group>> GetGroupsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(groups ?? new List<Group>
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
