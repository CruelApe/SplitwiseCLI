using SplitwiseCLI.Api;
using SplitwiseCLI.Configuration;
using SplitwiseCLI.Models;
using SplitwiseCLI.Services;
using Xunit;

namespace SplitwiseCLI.Tests;

public class ImportOrchestratorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"SplitwiseCLI.Tests-{Guid.NewGuid():N}");

    public ImportOrchestratorTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // Category/group are written as text so tests can exercise both valid numeric
    // ids (matching FakeSplitwiseClient's category id 101 / group id 55) and
    // deliberately invalid values (e.g. "NoSuchCategory", "999").
    private string WriteWorkbook(params (string Description, string Cost, string Date, string Category, string Group)[] rows)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add("Expenses");
        string[] headers = ["Description", "Cost", "Date", "Category", "Group"];
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
        }

        var path = Path.Combine(_root, $"{Guid.NewGuid():N}.xlsx");
        workbook.SaveAs(path);
        return path;
    }

    [Fact]
    public async Task RunAsync_ContinuesPastRowFailures_AndReportsBoth()
    {
        var file = WriteWorkbook(
            ("Good expense", "10.00", "2026-01-01", "101", "55"),
            ("Unknown category", "5.00", "2026-01-02", "999", "55"),
            ("Bad cost", "not-a-number", "2026-01-03", "101", "55"));

        var client = new FakeSplitwiseClient();
        var orchestrator = new ImportOrchestrator(
            client,
            new CategoryLookupService(client),
            new GroupLookupService(client),
            new AppConfig("key", "https://example.invalid", null));

        var results = await orchestrator.RunAsync([file]);

        Assert.Equal(3, results.Count);
        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(2, results.Count(r => !r.Success));
        Assert.Single(client.CreatedExpenses);
    }

    [Fact]
    public async Task CreateAsync_ReportsProgress_OnceForEachRow_InOrder()
    {
        var file = WriteWorkbook(
            ("Good expense", "10.00", "2026-01-01", "101", "55"),
            ("Unknown category", "5.00", "2026-01-02", "999", "55"),
            ("Another good expense", "5.00", "2026-01-03", "101", "55"));

        var client = new FakeSplitwiseClient();
        var orchestrator = new ImportOrchestrator(
            client, new CategoryLookupService(client), new GroupLookupService(client),
            new AppConfig("key", "https://example.invalid", null));

        var plan = await orchestrator.PrepareAsync([file]);
        var progressReports = new List<int>();

        var results = await orchestrator.CreateAsync(plan, onRowProcessed: progressReports.Add);

        Assert.Equal(3, results.Count);
        Assert.Equal([1, 2, 3], progressReports);
    }

    [Fact]
    public async Task CreateAsync_PopulatesRowDetails_ForSuccessfulRows()
    {
        var file = WriteWorkbook(("Good expense", "10.00", "2026-01-01", "101", "55"));

        var client = new FakeSplitwiseClient();
        var orchestrator = new ImportOrchestrator(
            client, new CategoryLookupService(client), new GroupLookupService(client),
            new AppConfig("key", "https://example.invalid", null));

        var results = await orchestrator.RunAsync([file]);

        var success = Assert.Single(results);
        Assert.True(success.Success);
        Assert.Equal("10.00", success.Cost);
        Assert.Equal(101, success.CategoryId);
        Assert.Equal(55, success.GroupId);
        Assert.StartsWith("SPLITWISE_CLI_", success.Details);
        Assert.StartsWith("2026-01-01", success.Date);
    }

    [Fact]
    public async Task RunAsync_TagsCreatedExpenses_WithBatchIdMatchingRowDates()
    {
        var file = WriteWorkbook(
            ("May expense", "10.00", "2026-05-15", "101", "55"),
            ("July expense", "20.00", "2026-07-01", "101", "55"));

        var client = new FakeSplitwiseClient();
        var orchestrator = new ImportOrchestrator(
            client,
            new CategoryLookupService(client),
            new GroupLookupService(client),
            new AppConfig("key", "https://example.invalid", null));

        var results = await orchestrator.RunAsync([file]);

        Assert.Equal(2, client.CreatedExpenses.Count);
        Assert.All(client.CreatedExpenses, r => Assert.StartsWith("SPLITWISE_CLI_202605-202607-", r.Details));

        var batchIds = results.Select(r => r.BatchId).Distinct().ToList();
        Assert.Single(batchIds);
        Assert.NotNull(batchIds[0]);
    }

    [Fact]
    public async Task PrepareAsync_AllValid_IsTrue_WhenEveryRowMapsCleanly()
    {
        var file = WriteWorkbook(
            ("Good expense", "10.00", "2026-01-01", "101", "55"),
            ("Another good expense", "5.00", "2026-01-02", "101", "55"));

        var client = new FakeSplitwiseClient();
        var orchestrator = new ImportOrchestrator(
            client, new CategoryLookupService(client), new GroupLookupService(client),
            new AppConfig("key", "https://example.invalid", null));

        var plan = await orchestrator.PrepareAsync([file]);

        Assert.True(plan.AllValid);
        Assert.Equal(2, plan.Rows.Count);
        Assert.Empty(client.CreatedExpenses); // PrepareAsync must never call CreateExpenseAsync
    }

    [Fact]
    public async Task PrepareAsync_AllValid_IsFalse_WhenAnyRowFailsValidationOrMapping()
    {
        var file = WriteWorkbook(
            ("Good expense", "10.00", "2026-01-01", "101", "55"),
            ("Unknown category", "5.00", "2026-01-02", "999", "55"));

        var client = new FakeSplitwiseClient();
        var orchestrator = new ImportOrchestrator(
            client, new CategoryLookupService(client), new GroupLookupService(client),
            new AppConfig("key", "https://example.invalid", null));

        var plan = await orchestrator.PrepareAsync([file]);

        Assert.False(plan.AllValid);
        Assert.Equal(2, plan.Rows.Count);
        Assert.Empty(client.CreatedExpenses);
    }

    [Fact]
    public async Task PrepareAsync_AllValid_IsFalse_WhenFileFailsToRead()
    {
        var missingFile = Path.Combine(_root, "does-not-exist.xlsx");

        var client = new FakeSplitwiseClient();
        var orchestrator = new ImportOrchestrator(
            client, new CategoryLookupService(client), new GroupLookupService(client),
            new AppConfig("key", "https://example.invalid", null));

        var plan = await orchestrator.PrepareAsync([missingFile]);

        Assert.False(plan.AllValid);
        var row = Assert.Single(plan.Rows);
        Assert.Null(row.Request);
        Assert.Contains("Failed to read file", row.Error);
    }

    [Fact]
    public async Task CreateAsync_CreatesOnlyValidRows_FromPreparedPlan()
    {
        var file = WriteWorkbook(
            ("Good expense", "10.00", "2026-01-01", "101", "55"),
            ("Unknown category", "5.00", "2026-01-02", "999", "55"));

        var client = new FakeSplitwiseClient();
        var orchestrator = new ImportOrchestrator(
            client, new CategoryLookupService(client), new GroupLookupService(client),
            new AppConfig("key", "https://example.invalid", null));

        var plan = await orchestrator.PrepareAsync([file]);
        var results = await orchestrator.CreateAsync(plan);

        Assert.Equal(2, results.Count);
        Assert.Single(client.CreatedExpenses);
        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(1, results.Count(r => !r.Success));
    }

    [Fact]
    public async Task RunAsync_GivesEachFile_ADifferentBatchId_EvenOverIdenticalDateRanges()
    {
        var file1 = WriteWorkbook(("Expense A", "10.00", "2026-05-15", "101", "55"));
        var file2 = WriteWorkbook(("Expense B", "20.00", "2026-05-16", "101", "55"));

        var client = new FakeSplitwiseClient();
        var orchestrator = new ImportOrchestrator(
            client,
            new CategoryLookupService(client),
            new GroupLookupService(client),
            new AppConfig("key", "https://example.invalid", null));

        var results = await orchestrator.RunAsync([file1, file2]);

        var batchIds = results.Select(r => r.BatchId).Distinct().ToList();
        Assert.Equal(2, batchIds.Count);
    }

    private sealed class FakeSplitwiseClient : ISplitwiseClient
    {
        public List<CreateExpenseRequest> CreatedExpenses { get; } = [];

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
            CreatedExpenses.Add(request);
            return Task.FromResult(new CreateExpenseResponse { Success = true, Expenses = [new CreatedExpense { Id = CreatedExpenses.Count }] });
        }

        public Task DeleteExpenseAsync(long id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
