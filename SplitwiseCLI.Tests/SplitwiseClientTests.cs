using RichardSzalay.MockHttp;
using SplitwiseCLI.Api;
using SplitwiseCLI.Configuration;
using SplitwiseCLI.Models;
using Xunit;

namespace SplitwiseCLI.Tests;

public class SplitwiseClientTests
{
    private static readonly AppConfig Config = new("test-api-key", "https://secure.splitwise.com/api/v3.0", null);

    [Fact]
    public async Task GetCategoriesAsync_SendsBearerAuthorizationHeader()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.Expect(HttpMethod.Get, "https://secure.splitwise.com/api/v3.0/get_categories")
            .With(req => req.Headers.Authorization?.Scheme == "Bearer" && req.Headers.Authorization.Parameter == "test-api-key")
            .Respond("application/json", """{"categories":[]}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);

        await client.GetCategoriesAsync();

        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetCategoriesAsync_DeserializesParentAndSubcategories()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://secure.splitwise.com/api/v3.0/get_categories")
            .Respond("application/json", """
                {"categories":[{"id":1,"name":"Food","subcategories":[{"id":10,"name":"Groceries"},{"id":11,"name":"Dining out"}]}]}
                """);

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);

        var categories = await client.GetCategoriesAsync();

        var food = Assert.Single(categories);
        Assert.Equal("Food", food.Name);
        Assert.Equal(2, food.Subcategories.Count);
        Assert.Contains(food.Subcategories, s => s.Id == 10 && s.Name == "Groceries");
    }

    [Fact]
    public async Task CreateExpenseAsync_TreatsHttp200WithSuccessFalse_AsFailure()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Post, "https://secure.splitwise.com/api/v3.0/create_expense")
            .Respond("application/json", """{"success":false,"errors":{"base":["Invalid category_id"]}}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        var request = new CreateExpenseRequest
        {
            Cost = "10.00",
            Description = "Test",
            Date = "2026-01-15T00:00:00Z",
            CurrencyCode = "USD",
            CategoryId = 999999,
            GroupId = 1,
        };

        var ex = await Assert.ThrowsAsync<SplitwiseApiException>(() => client.CreateExpenseAsync(request));
        Assert.Contains("Invalid category_id", ex.Message);
    }

    [Fact]
    public async Task CreateExpenseAsync_ReturnsResponse_OnSuccess()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Post, "https://secure.splitwise.com/api/v3.0/create_expense")
            .Respond("application/json", """{"success":true,"expenses":[{"id":123,"cost":"10.00","description":"Test"}]}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);
        var request = new CreateExpenseRequest
        {
            Cost = "10.00",
            Description = "Test",
            Date = "2026-01-15T00:00:00Z",
            CurrencyCode = "USD",
            CategoryId = 10,
            GroupId = 1,
        };

        var response = await client.CreateExpenseAsync(request);

        Assert.True(response.Success);
        Assert.Equal(123, response.Expenses?[0].Id);
    }

    [Fact]
    public async Task GetCategoriesAsync_ThrowsSplitwiseApiException_OnNon2xxStatus()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://secure.splitwise.com/api/v3.0/get_categories")
            .Respond(System.Net.HttpStatusCode.Unauthorized, "application/json", """{"error":"Invalid API key"}""");

        var client = new SplitwiseClient(mockHttp.ToHttpClient(), Config);

        await Assert.ThrowsAsync<SplitwiseApiException>(() => client.GetCategoriesAsync());
    }
}
