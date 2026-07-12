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
            ("Good expense", "10.00", "2026-01-01", "Groceries", "Roommates"),
            ("Unknown category", "5.00", "2026-01-02", "NoSuchCategory", "Roommates"),
            ("Bad cost", "not-a-number", "2026-01-03", "Groceries", "Roommates"));

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
    }
}
