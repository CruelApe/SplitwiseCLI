using SplitwiseCLI.Import;
using SplitwiseCLI.Models;
using Xunit;

namespace SplitwiseCLI.Tests;

public class ExpenseMapperTests
{
    private static ValidatedExpenseRow ValidRow(long categoryId = 101, long groupId = 55, string? details = null) => new()
    {
        Description = "Groceries",
        Details = details,
        Cost = 42.5m,
        Date = new DateTime(2026, 1, 15),
        CategoryId = categoryId,
        GroupId = groupId,
    };

    private static Dictionary<long, string> CategoryLookup() => new() { [101] = "Groceries" };

    private static Dictionary<long, Group> GroupLookup() => new()
    {
        [55] = new Group { Id = 55, Name = "Roommates", Members = [new GroupMember { Id = 1 }, new GroupMember { Id = 2 }] },
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
    public void Map_PassesThroughDetails()
    {
        var (request, error) = ExpenseMapper.Map(ValidRow(details: "Weekly shop"), CategoryLookup(), GroupLookup(), "USD");

        Assert.Null(error);
        Assert.Equal("Weekly shop", request!.Details);
    }

    [Fact]
    public void Map_LeavesDetailsNull_WhenNotProvided()
    {
        var (request, error) = ExpenseMapper.Map(ValidRow(), CategoryLookup(), GroupLookup(), "USD");

        Assert.Null(error);
        Assert.Null(request!.Details);
    }

    [Fact]
    public void Map_FailsForUnknownCategoryId()
    {
        var (request, error) = ExpenseMapper.Map(ValidRow(categoryId: 999), CategoryLookup(), GroupLookup(), "USD");

        Assert.Null(request);
        Assert.Contains("999", error);
    }

    [Fact]
    public void Map_FailsForUnknownGroupId()
    {
        var (request, error) = ExpenseMapper.Map(ValidRow(groupId: 999), CategoryLookup(), GroupLookup(), "USD");

        Assert.Null(request);
        Assert.Contains("999", error);
    }

    [Fact]
    public void Map_OverwritesExistingDetails_WithJustTheBatchTag()
    {
        var (request, error) = ExpenseMapper.Map(
            ValidRow(details: "Weekly shop"), CategoryLookup(), GroupLookup(), "USD", batchId: "202605-202607-a1b2c3");

        Assert.Null(error);
        Assert.Equal("SPLITWISE_CLI_202605-202607-a1b2c3", request!.Details);
    }

    [Fact]
    public void Map_UsesBatchTagAsOnlyDetails_WhenNoDetailsProvided()
    {
        var (request, error) = ExpenseMapper.Map(ValidRow(), CategoryLookup(), GroupLookup(), "USD", batchId: "202605-202607-a1b2c3");

        Assert.Null(error);
        Assert.Equal("SPLITWISE_CLI_202605-202607-a1b2c3", request!.Details);
    }

    [Fact]
    public void Map_LeavesDetailsUnchanged_WhenBatchIdOmitted()
    {
        var (request, error) = ExpenseMapper.Map(ValidRow(details: "Weekly shop"), CategoryLookup(), GroupLookup(), "USD");

        Assert.Null(error);
        Assert.Equal("Weekly shop", request!.Details);
    }

    [Fact]
    public void Map_FailsForGroupWithNoMembers()
    {
        var groupLookup = new Dictionary<long, Group>
        {
            [9] = new Group { Id = 9, Name = "Empty", Members = [] },
        };

        var (request, error) = ExpenseMapper.Map(ValidRow(groupId: 9), CategoryLookup(), groupLookup, "USD");

        Assert.Null(request);
        Assert.NotNull(error);
    }
}
