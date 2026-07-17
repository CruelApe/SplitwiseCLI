using SplitwiseCLI.Import;
using SplitwiseCLI.Models;
using Xunit;

namespace SplitwiseCLI.Tests;

public class ExpenseMapperTests
{
    private static ValidatedExpenseRow ValidRow(string category = "Groceries", string group = "Roommates") => new()
    {
        Description = "Groceries",
        Cost = 42.5m,
        Date = new DateTime(2026, 1, 15),
        Category = category,
        Group = group,
    };

    private static Dictionary<string, long> CategoryLookup() =>
        new(StringComparer.OrdinalIgnoreCase) { ["Groceries"] = 101 };

    private static Dictionary<string, Group> GroupLookup() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Roommates"] = new Group { Id = 55, Name = "Roommates", Members = [new GroupMember { Id = 1 }, new GroupMember { Id = 2 }] },
    };

    [Fact]
    public void Map_ProducesRequest_ForKnownCategoryAndGroup()
    {
        var (request, error) = ExpenseMapper.Map(ValidRow(), CategoryLookup(), GroupLookup(), "USD");

        Assert.Null(error);
        Assert.NotNull(request);
        Assert.Equal("42.50", request!.Cost);
        Assert.Equal(101, request.CategoryId);
        Assert.Equal(55, request.GroupId);
        Assert.Equal("USD", request.CurrencyCode);
        Assert.True(request.SplitEqually);
    }

    [Fact]
    public void Map_IsCaseInsensitive_ForCategoryAndGroupNames()
    {
        var (request, error) = ExpenseMapper.Map(ValidRow("groceries", "roommates"), CategoryLookup(), GroupLookup(), "USD");

        Assert.Null(error);
        Assert.NotNull(request);
    }

    [Fact]
    public void Map_FailsForUnknownCategory()
    {
        var (request, error) = ExpenseMapper.Map(ValidRow(category: "Nonexistent"), CategoryLookup(), GroupLookup(), "USD");

        Assert.Null(request);
        Assert.Contains("Nonexistent", error);
    }

    [Fact]
    public void Map_FailsForUnknownGroup()
    {
        var (request, error) = ExpenseMapper.Map(ValidRow(group: "Nonexistent"), CategoryLookup(), GroupLookup(), "USD");

        Assert.Null(request);
        Assert.Contains("Nonexistent", error);
    }

    [Fact]
    public void Map_FailsForGroupWithNoMembers()
    {
        var groupLookup = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase)
        {
            ["Empty"] = new Group { Id = 9, Name = "Empty", Members = [] },
        };

        var (request, error) = ExpenseMapper.Map(ValidRow(group: "Empty"), CategoryLookup(), groupLookup, "USD");

        Assert.Null(request);
        Assert.NotNull(error);
    }
}
