using SplitwiseCLI.Api;
using SplitwiseCLI.Models;
using SplitwiseCLI.Services;
using Xunit;

namespace SplitwiseCLI.Tests;

public class RollbackOrchestratorTests
{
    private const string BatchIdValue = "202605-202607-a1b2c3";

    private static Expense TaggedExpense(long id, string batchId = BatchIdValue, string? description = null) => new()
    {
        Id = id,
        Description = description ?? $"Expense {id}",
        Details = $"SPLITWISE_CLI_{batchId}",
    };

    [Fact]
    public async Task FindMatchesAsync_ReturnsOnlyExactTagMatches()
    {
        var client = new FakeSplitwiseClient
        {
            Pages =
            [
                [
                    TaggedExpense(1),
                    TaggedExpense(2, batchId: "202605-202607-ffffff"), // same date-range prefix, different suffix
                    new Expense { Id = 3, Description = "Untagged", Details = null },
                    new Expense { Id = 4, Description = "Deleted match", Details = $"SPLITWISE_CLI_{BatchIdValue}", Deleted = true },
                ],
            ],
        };
        var orchestrator = new RollbackOrchestrator(client, new ExpenseSearchService(client));

        var matches = await orchestrator.FindMatchesAsync(BatchIdValue);

        Assert.Single(matches);
        Assert.Equal(1, matches[0].Id);
    }

    [Fact]
    public async Task FindMatchesAsync_PaginatesUntilShortPage()
    {
        var fullPage = Enumerable.Range(1, 100).Select(i => TaggedExpense(i)).ToList();
        var shortPage = new List<Expense> { TaggedExpense(101), TaggedExpense(102) };

        var client = new FakeSplitwiseClient { Pages = [fullPage, shortPage] };
        var orchestrator = new RollbackOrchestrator(client, new ExpenseSearchService(client));

        var matches = await orchestrator.FindMatchesAsync(BatchIdValue);

        Assert.Equal(102, matches.Count);
        Assert.Equal([0, 100], client.RequestedOffsets);
    }

    [Fact]
    public async Task FindMatchesAsync_ReportsScannedCount_PerPage()
    {
        var fullPage = Enumerable.Range(1, 100).Select(i => TaggedExpense(i)).ToList();
        var shortPage = new List<Expense> { TaggedExpense(101), TaggedExpense(102) };

        var client = new FakeSplitwiseClient { Pages = [fullPage, shortPage] };
        var orchestrator = new RollbackOrchestrator(client, new ExpenseSearchService(client));
        var scannedReports = new List<int>();

        await orchestrator.FindMatchesAsync(BatchIdValue, onExpensesScanned: scannedReports.Add);

        Assert.Equal([100, 102], scannedReports);
    }

    [Fact]
    public async Task FindMatchesAsync_Throws_ForMalformedBatchId()
    {
        var client = new FakeSplitwiseClient();
        var orchestrator = new RollbackOrchestrator(client, new ExpenseSearchService(client));

        await Assert.ThrowsAsync<ArgumentException>(() => orchestrator.FindMatchesAsync("not-a-batch-id"));
        Assert.Empty(client.RequestedOffsets);
    }

    [Fact]
    public async Task DeleteAsync_ContinuesPastOneFailure_AndReportsBoth()
    {
        var client = new FakeSplitwiseClient { FailingIds = { 2 } };
        var orchestrator = new RollbackOrchestrator(client, new ExpenseSearchService(client));
        var matches = new List<Expense> { TaggedExpense(1), TaggedExpense(2) };

        var results = await orchestrator.DeleteAsync(matches);

        Assert.Equal(2, results.Count);
        Assert.True(results.Single(r => r.ExpenseId == 1).Success);
        Assert.False(results.Single(r => r.ExpenseId == 2).Success);
        Assert.Contains(1L, client.DeletedIds);
        Assert.DoesNotContain(2L, client.DeletedIds);
    }

    [Fact]
    public async Task DeleteAsync_ReportsProgress_OnceForEachExpense_InOrder()
    {
        var client = new FakeSplitwiseClient();
        var orchestrator = new RollbackOrchestrator(client, new ExpenseSearchService(client));
        var matches = new List<Expense> { TaggedExpense(1), TaggedExpense(2), TaggedExpense(3) };
        var progressReports = new List<int>();

        var results = await orchestrator.DeleteAsync(matches, onExpenseProcessed: progressReports.Add);

        Assert.Equal(3, results.Count);
        Assert.Equal([1, 2, 3], progressReports);
    }

    private sealed class FakeSplitwiseClient : ISplitwiseClient
    {
        public List<List<Expense>> Pages { get; init; } = [];
        public List<int?> RequestedOffsets { get; } = [];
        public List<long> DeletedIds { get; } = [];
        public HashSet<long> FailingIds { get; init; } = [];

        public Task<List<Expense>> GetExpensesAsync(ExpenseFilter filter, CancellationToken cancellationToken = default)
        {
            var callIndex = RequestedOffsets.Count;
            RequestedOffsets.Add(filter.Offset);
            return Task.FromResult(callIndex < Pages.Count ? Pages[callIndex] : []);
        }

        public Task DeleteExpenseAsync(long id, CancellationToken cancellationToken = default)
        {
            if (FailingIds.Contains(id))
            {
                throw new SplitwiseApiException($"Failed to delete expense {id}.");
            }

            DeletedIds.Add(id);
            return Task.CompletedTask;
        }

        public Task<User> GetCurrentUserAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<User> GetUserAsync(long id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Group>> GetGroupsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Group> GetGroupAsync(long id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Friend>> GetFriendsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Friend> GetFriendAsync(long id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Expense> GetExpenseAsync(long id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Comment>> GetCommentsAsync(long expenseId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Notification>> GetNotificationsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Currency>> GetCurrenciesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CreateExpenseResponse> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
