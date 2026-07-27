using SplitwiseCLI.Api;
using SplitwiseCLI.Configuration;
using SplitwiseCLI.Models;
using SplitwiseCLI.Services;
using Xunit;

namespace SplitwiseCLI.Tests;

public class CurrencyResolverTests
{
    [Fact]
    public async Task ResolveAsync_PrefersOverride_OverAccountDefault()
    {
        var client = new FakeSplitwiseClient("USD");
        var config = new AppConfig("key", "https://example.invalid", "EUR");

        var currency = await CurrencyResolver.ResolveAsync(client, config);

        Assert.Equal("EUR", currency);
    }

    [Fact]
    public async Task ResolveAsync_FallsBackToAccountDefault_WhenNoOverride()
    {
        var client = new FakeSplitwiseClient("USD");
        var config = new AppConfig("key", "https://example.invalid", null);

        var currency = await CurrencyResolver.ResolveAsync(client, config);

        Assert.Equal("USD", currency);
    }

    [Fact]
    public async Task ResolveAsync_Throws_WhenNeitherOverrideNorAccountDefaultExists()
    {
        var client = new FakeSplitwiseClient(null);
        var config = new AppConfig("key", "https://example.invalid", null);

        await Assert.ThrowsAsync<SplitwiseApiException>(() => CurrencyResolver.ResolveAsync(client, config));
    }

    private sealed class FakeSplitwiseClient(string? defaultCurrency) : ISplitwiseClient
    {
        public Task<User> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new User { Id = 1, DefaultCurrency = defaultCurrency });

        public Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<List<Group>> GetGroupsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<User> GetUserAsync(long id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<Group> GetGroupAsync(long id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<List<Friend>> GetFriendsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<Friend> GetFriendAsync(long id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<List<Expense>> GetExpensesAsync(ExpenseFilter filter, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<Expense> GetExpenseAsync(long id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<List<Comment>> GetCommentsAsync(long expenseId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<List<Notification>> GetNotificationsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<List<Currency>> GetCurrenciesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<CreateExpenseResponse> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task DeleteExpenseAsync(long id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
