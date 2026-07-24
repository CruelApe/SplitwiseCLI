using System.Text.RegularExpressions;
using SplitwiseCLI.Import;
using Xunit;

namespace SplitwiseCLI.Tests;

public class BatchIdTests
{
    private static readonly Regex FullFormat = new(@"^\d{6}-\d{6}-[0-9a-f]{6}$");

    [Fact]
    public void Generate_ProducesYyyyMMDashYyyyMM_Prefix_ForMultiMonthRange()
    {
        var id = BatchId.Generate([new DateTime(2026, 5, 3), new DateTime(2026, 7, 20), new DateTime(2026, 6, 1)]);

        Assert.StartsWith("202605-202607-", id);
        Assert.Matches(FullFormat, id);
    }

    [Fact]
    public void Generate_RepeatsSameMonth_ForSingleMonthRange()
    {
        var id = BatchId.Generate([new DateTime(2026, 6, 1), new DateTime(2026, 6, 30)]);

        Assert.StartsWith("202606-202606-", id);
    }

    [Fact]
    public void Generate_ProducesDifferentSuffixes_AcrossCalls_OverIdenticalRange()
    {
        var dates = new[] { new DateTime(2026, 5, 1), new DateTime(2026, 7, 1) };

        var first = BatchId.Generate(dates);
        var second = BatchId.Generate(dates);

        Assert.NotEqual(first, second);
        Assert.Equal(first.Split('-')[..2], second.Split('-')[..2]);
    }

    [Fact]
    public void Generate_Throws_ForEmptyDates()
    {
        Assert.Throws<ArgumentException>(() => BatchId.Generate([]));
    }

    [Fact]
    public void TryParseDateRange_RoundTrips_ForGeneratedId()
    {
        var id = BatchId.Generate([new DateTime(2026, 5, 3), new DateTime(2026, 7, 20)]);

        var parsed = BatchId.TryParseDateRange(id, out var start, out var end);

        Assert.True(parsed);
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 7, 31, 23, 59, 59, TimeSpan.Zero).AddTicks(TimeSpan.TicksPerSecond - 1), end);
    }

    [Theory]
    [InlineData("not-a-batch-id")]
    [InlineData("202605-202607")]
    [InlineData("202605-202607-zzzzzz")]
    [InlineData("202513-202607-a1b2c3")]
    [InlineData("202605-202607-a1b2c")]
    public void TryParseDateRange_ReturnsFalse_ForMalformedInput(string input)
    {
        Assert.False(BatchId.TryParseDateRange(input, out _, out _));
    }

    [Fact]
    public void ApplyTag_OverwritesExistingDetails_WithJustTheTag()
    {
        var result = BatchId.ApplyTag("Weekly shop", "202605-202607-a1b2c3");

        Assert.Equal("SPLITWISE_CLI_202605-202607-a1b2c3", result);
    }

    [Fact]
    public void ApplyTag_SetsTagAsOnlyDetails_WhenDetailsNullOrBlank()
    {
        Assert.Equal("SPLITWISE_CLI_202605-202607-a1b2c3", BatchId.ApplyTag(null, "202605-202607-a1b2c3"));
        Assert.Equal("SPLITWISE_CLI_202605-202607-a1b2c3", BatchId.ApplyTag("   ", "202605-202607-a1b2c3"));
    }

    [Fact]
    public void ApplyTag_ReturnsOriginalDetails_WhenBatchIdNull()
    {
        Assert.Equal("Weekly shop", BatchId.ApplyTag("Weekly shop", null));
        Assert.Null(BatchId.ApplyTag(null, null));
    }

    [Fact]
    public void HasTag_MatchesExactTaggedDetails()
    {
        Assert.True(BatchId.HasTag("Weekly shop | SPLITWISE_CLI_202605-202607-a1b2c3", "202605-202607-a1b2c3"));
    }

    [Fact]
    public void HasTag_DoesNotMatch_SameDateRangePrefix_DifferentSuffix()
    {
        Assert.False(BatchId.HasTag("SPLITWISE_CLI_202605-202607-a1b2c3", "202605-202607-ffffff"));
    }
}
