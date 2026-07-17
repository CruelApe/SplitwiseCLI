using RichardSzalay.MockHttp;
using SplitwiseCLI.Api;
using SplitwiseCLI.Configuration;
using SplitwiseCLI.Models;
using Xunit;

namespace SplitwiseCLI.Tests;

public class SplitwiseClientGetEndpointsTests
{
    private const string BaseUrl = "https://secure.splitwise.com/api/v3.0";
    private static readonly AppConfig Config = new("test-api-key", BaseUrl, null);

    [Fact]
    public async Task GetUserAsync_RequestsUserById_AndDeserializes()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.Expect($"{BaseUrl}/get_user/42")
            .Respond("application/json", """{"user":{"id":42,"first_name":"Ada","last_name":"Lovelace","email":"ada@example.com"}}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        var user = await client.GetUserAsync(42);

        Assert.Equal(42, user.Id);
        Assert.Equal("Ada", user.FirstName);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetGroupAsync_RequestsGroupById_AndDeserializesMembers()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.Expect($"{BaseUrl}/get_group/7")
            .Respond("application/json", """{"group":{"id":7,"name":"Roommates","members":[{"id":1,"first_name":"Ada"}]}}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        var group = await client.GetGroupAsync(7);

        Assert.Equal("Roommates", group.Name);
        Assert.Single(group.Members);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetFriendsAsync_DeserializesListWithBalances()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When($"{BaseUrl}/get_friends")
            .Respond("application/json", """{"friends":[{"id":9,"first_name":"Grace","balance":[{"currency_code":"USD","amount":"12.50"}]}]}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        var friends = await client.GetFriendsAsync();

        var friend = Assert.Single(friends);
        Assert.Equal("Grace", friend.FirstName);
        Assert.Equal("12.50", friend.Balance[0].Amount);
    }

    [Fact]
    public async Task GetFriendAsync_RequestsFriendById()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.Expect($"{BaseUrl}/get_friend/9")
            .Respond("application/json", """{"friend":{"id":9,"first_name":"Grace"}}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        var friend = await client.GetFriendAsync(9);

        Assert.Equal(9, friend.Id);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetExpensesAsync_WithNoFilter_RequestsBareEndpoint()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.Expect($"{BaseUrl}/get_expenses")
            .Respond("application/json", """{"expenses":[]}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        await client.GetExpensesAsync(new ExpenseFilter());

        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetExpensesAsync_WithFilters_BuildsQueryString()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.Expect(HttpMethod.Get, $"{BaseUrl}/get_expenses")
            .WithQueryString("group_id", "7")
            .WithQueryString("limit", "5")
            .Respond("application/json", """{"expenses":[]}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        await client.GetExpensesAsync(new ExpenseFilter(GroupId: 7, Limit: 5));

        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetExpensesAsync_DeserializesUsersAndRepayments()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When($"{BaseUrl}/get_expenses")
            .Respond("application/json", """
                {"expenses":[{"id":1,"description":"Dinner","cost":"20.00","currency_code":"USD",
                "users":[{"user":{"id":1,"first_name":"Ada"},"paid_share":"20.00","owed_share":"10.00","net_balance":"10.00"}],
                "repayments":[{"from":2,"to":1,"amount":"10.00"}]}]}
                """);

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        var expenses = await client.GetExpensesAsync(new ExpenseFilter());

        var expense = Assert.Single(expenses);
        Assert.Equal("Dinner", expense.Description);
        Assert.Single(expense.Users);
        Assert.Single(expense.Repayments);
        Assert.Equal("10.00", expense.Repayments[0].Amount);
    }

    [Fact]
    public async Task GetExpenseAsync_RequestsExpenseById()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.Expect($"{BaseUrl}/get_expense/55")
            .Respond("application/json", """{"expense":{"id":55,"description":"Lunch"}}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        var expense = await client.GetExpenseAsync(55);

        Assert.Equal("Lunch", expense.Description);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetCommentsAsync_IncludesExpenseIdQueryParam()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.Expect(HttpMethod.Get, $"{BaseUrl}/get_comments")
            .WithQueryString("expense_id", "55")
            .Respond("application/json", """{"comments":[{"id":1,"content":"Thanks!","user":{"id":1,"first_name":"Ada"}}]}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        var comments = await client.GetCommentsAsync(55);

        var comment = Assert.Single(comments);
        Assert.Equal("Thanks!", comment.Content);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetNotificationsAsync_DeserializesList()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When($"{BaseUrl}/get_notifications")
            .Respond("application/json", """{"notifications":[{"id":1,"type":0,"content":"Added an expense"}]}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        var notifications = await client.GetNotificationsAsync();

        Assert.Single(notifications);
    }

    [Fact]
    public async Task GetCurrenciesAsync_DeserializesBareArray()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When($"{BaseUrl}/get_currencies")
            .Respond("application/json", """[{"currency_code":"USD","unit":"$"},{"currency_code":"EUR","unit":"€"}]""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        var currencies = await client.GetCurrenciesAsync();

        Assert.Equal(2, currencies.Count);
        Assert.Contains(currencies, c => c.CurrencyCode == "USD");
    }

    [Fact]
    public async Task GetCurrenciesAsync_DeserializesObjectKeyedByCode_WithStringUnits()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When($"{BaseUrl}/get_currencies")
            .Respond("application/json", """{"USD":"$","EUR":"€"}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        var currencies = await client.GetCurrenciesAsync();

        Assert.Equal(2, currencies.Count);
        Assert.Contains(currencies, c => c.CurrencyCode == "USD" && c.Unit == "$");
    }

    [Fact]
    public async Task GetCurrenciesAsync_DeserializesObjectKeyedByCode_WithNestedUnitObjects()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When($"{BaseUrl}/get_currencies")
            .Respond("application/json", """{"USD":{"currency_code":"USD","unit":"$"}}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        var currencies = await client.GetCurrenciesAsync();

        var currency = Assert.Single(currencies);
        Assert.Equal("USD", currency.CurrencyCode);
        Assert.Equal("$", currency.Unit);
    }

    [Fact]
    public async Task GetExpensesAsync_DeserializesQuotedNumericId()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When($"{BaseUrl}/get_expenses")
            .Respond("application/json", """{"expenses":[{"id":"123","description":"Dinner"}]}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        var expenses = await client.GetExpensesAsync(new ExpenseFilter());

        Assert.Equal(123, Assert.Single(expenses).Id);
    }
}
