using SplitwiseCLI.Import;
using SplitwiseCLI.Models;
using SplitwiseCLI.Services;
using Xunit;

namespace SplitwiseCLI.Tests;

public class DuplicateExpenseDetectorTests
{
    private static ValidatedExpenseRow Row(string description, decimal cost, DateTime date, long categoryId, long groupId) => new()
    {
        Description = description,
        Cost = cost,
        Date = date,
        CategoryId = categoryId,
        GroupId = groupId,
    };

    private static Expense Candidate(long id, string description, string cost, DateTime date, long categoryId, long groupId, bool? deleted = null) => new()
    {
        Id = id,
        Description = description,
        Cost = cost,
        Date = new DateTimeOffset(date, TimeSpan.Zero),
        Category = new ExpenseCategoryRef { Id = categoryId },
        GroupId = groupId,
        Deleted = deleted,
    };

    [Fact]
    public void FindDuplicateReason_ReturnsExactDuplicate_WhenEveryFieldMatches()
    {
        var row = Row("Groceries", 10.00m, new DateTime(2026, 1, 1), 101, 55);
        var candidates = new[] { Candidate(1, "Groceries", "10.00", new DateTime(2026, 1, 1), 101, 55) };

        var reason = DuplicateExpenseDetector.FindDuplicateReason(row, candidates);

        Assert.NotNull(reason);
        Assert.Contains("Exact duplicate", reason);
        Assert.Contains("#1", reason);
    }

    [Fact]
    public void FindDuplicateReason_ReturnsPossibleDuplicate_WhenDatesAreCloseButNotAWeekApart()
    {
        var row = Row("Groceries", 10.00m, new DateTime(2026, 1, 4), 101, 55);
        var candidates = new[] { Candidate(1, "Groceries", "10.00", new DateTime(2026, 1, 1), 101, 55) };

        var reason = DuplicateExpenseDetector.FindDuplicateReason(row, candidates);

        Assert.NotNull(reason);
        Assert.Contains("Possible duplicate", reason);
    }

    [Fact]
    public void FindDuplicateReason_ReturnsNull_WhenCandidateIsAWeekBeforeTheRow()
    {
        var row = Row("Groceries", 10.00m, new DateTime(2026, 1, 8), 101, 55);
        var candidates = new[] { Candidate(1, "Groceries", "10.00", new DateTime(2026, 1, 1), 101, 55) };

        var reason = DuplicateExpenseDetector.FindDuplicateReason(row, candidates);

        Assert.Null(reason);
    }

    [Fact]
    public void FindDuplicateReason_ReturnsNull_WhenCandidateIsAWeekAfterTheRow()
    {
        // Same check as the "before" case, but with the existing expense dated later
        // than the imported row - the exemption must cover both directions.
        var row = Row("Groceries", 10.00m, new DateTime(2026, 1, 1), 101, 55);
        var candidates = new[] { Candidate(1, "Groceries", "10.00", new DateTime(2026, 1, 8), 101, 55) };

        var reason = DuplicateExpenseDetector.FindDuplicateReason(row, candidates);

        Assert.Null(reason);
    }

    [Fact]
    public void FindDuplicateReason_ReturnsNull_WhenDatesAreTwoWeeksApart()
    {
        // 7 days+ - any exact multiple of a week is a legitimate recurring charge,
        // not just a single week.
        var row = Row("Groceries", 10.00m, new DateTime(2026, 1, 15), 101, 55);
        var candidates = new[] { Candidate(1, "Groceries", "10.00", new DateTime(2026, 1, 1), 101, 55) };

        var reason = DuplicateExpenseDetector.FindDuplicateReason(row, candidates);

        Assert.Null(reason);
    }

    [Fact]
    public void FindDuplicateReason_FlagsDuplicate_WhenGapIsTenDays()
    {
        // Close to two weeks but not an exact multiple of 7 - still a possible duplicate.
        var row = Row("Groceries", 10.00m, new DateTime(2026, 1, 11), 101, 55);
        var candidates = new[] { Candidate(1, "Groceries", "10.00", new DateTime(2026, 1, 1), 101, 55) };

        var reason = DuplicateExpenseDetector.FindDuplicateReason(row, candidates);

        Assert.NotNull(reason);
        Assert.Contains("Possible duplicate", reason);
    }

    [Theory]
    [InlineData("Different description", "10.00", 101, 55)]
    [InlineData("Groceries", "20.00", 101, 55)]
    [InlineData("Groceries", "10.00", 202, 55)]
    [InlineData("Groceries", "10.00", 101, 66)]
    public void FindDuplicateReason_ReturnsNull_WhenAnyFieldDiffers(string description, string cost, long categoryId, long groupId)
    {
        var row = Row("Groceries", 10.00m, new DateTime(2026, 1, 1), 101, 55);
        var candidates = new[] { Candidate(1, description, cost, new DateTime(2026, 1, 1), categoryId, groupId) };

        var reason = DuplicateExpenseDetector.FindDuplicateReason(row, candidates);

        Assert.Null(reason);
    }

    [Fact]
    public void FindDuplicateReason_IgnoresDeletedCandidatesPassedIn()
    {
        // FindCandidatesAsync filters deleted expenses out before this is called, but
        // the matcher itself is deliberately permissive - this documents that callers
        // are responsible for filtering, not FindDuplicateReason.
        var row = Row("Groceries", 10.00m, new DateTime(2026, 1, 1), 101, 55);
        var candidates = new[] { Candidate(1, "Groceries", "10.00", new DateTime(2026, 1, 1), 101, 55, deleted: true) };

        var reason = DuplicateExpenseDetector.FindDuplicateReason(row, candidates);

        Assert.NotNull(reason);
    }
}
