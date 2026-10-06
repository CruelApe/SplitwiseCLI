using SplitwiseCLI.Statements;
using Xunit;

namespace SplitwiseCLI.Tests;

public class LatitudeGoStatementParserTests
{
    private readonly LatitudeGoStatementParser _parser = new();

    [Fact]
    public void CanParse_TrueForMatchingHeader_FalseOtherwise()
    {
        const string Header = "Date Card Description Debits Credits";
        Assert.True(_parser.CanParse(Header));
        Assert.False(_parser.CanParse("Processed Date Transaction Date Details Amount"));
    }

    [Fact]
    public void Parse_ExtractsDebitRow_WithDollarAmount()
    {
        const string Text = """
            Date Card Description Debits Credits
            01/05/2026 1234 Coles Supermarket Greenvale $45.67
            """;

        var rows = _parser.Parse("statement.pdf", Text).Rows;

        var row = Assert.Single(rows);
        Assert.Equal("Coles Supermarket Greenvale", row.Description);
        Assert.Equal(45.67m, row.Cost);
        Assert.Equal(new DateTime(2026, 5, 1), row.Date);
        Assert.Null(row.CategoryId);
        Assert.Null(row.GroupId);
    }

    [Fact]
    public void Parse_IgnoresBpayPaymentReceivedLines_EvenWithADebitAmount()
    {
        const string Text = """
            Date Card Description Debits Credits
            03/05/2026 1234 BPAY Payment Received Thank You $100.00
            """;

        var rows = _parser.Parse("statement.pdf", Text).Rows;

        Assert.Empty(rows);
    }

    [Fact]
    public void Parse_HandlesThousandsSeparatorInAmount()
    {
        const string Text = """
            Date Card Description Debits Credits
            04/05/2026 1234 Furniture Store $1,234.56
            """;

        var rows = _parser.Parse("statement.pdf", Text).Rows;

        var row = Assert.Single(rows);
        Assert.Equal(1234.56m, row.Cost);
    }

    // The layout PdfPig extracts for an international purchase on a real statement -
    // the description line has no amount, and the total sits alone three lines later.
    [Fact]
    public void Parse_ExtractsInternationalTransaction_WrappedOverSeveralLines()
    {
        const string Text = """
            Date Card Description Debits Credits
            01/07/2026 1234 Amazon Au Marketplace Sydney Aus $26.99
            04/07/2026 1234 Anthropic* Claude Sub San Francisco Ca
            34.00 AUD Rate:1.000000
            Txn Inc 1.02 International Transaction Fee
            $35.02
            04/07/2026 1234 Coles 0608 Greenvale Vic $55.35
            """;

        var result = _parser.Parse("statement.pdf", Text);

        Assert.Empty(result.Issues);
        Assert.Collection(
            result.Rows,
            row => Assert.Equal((26.99m, "Amazon Au Marketplace Sydney Aus"), (row.Cost, row.Description)),
            row =>
            {
                Assert.Equal("Anthropic* Claude Sub San Francisco Ca", row.Description);
                Assert.Equal(35.02m, row.Cost);
                Assert.Equal(new DateTime(2026, 7, 4), row.Date);
            },
            row => Assert.Equal((55.35m, "Coles 0608 Greenvale Vic"), (row.Cost, row.Description)));
    }

    [Fact]
    public void Parse_ExtractsWrappedTransaction_SplitAcrossAPageBreak()
    {
        const string Text = """
            Date Card Description Debits Credits
            24/07/2026 1234 Anthropic* Claude Sub San Francisco Ca
            34.00 AUD Rate:1.000000
            Statement date 01/08/2026
            Page 3 of 5
            Transactions this statement period
            Your transactionsContinued...
            Date Card Description Debits Credits
            Txn Inc 1.02 International Transaction Fee
            $35.02
            26/07/2026 1234 Coles 0608 Greenvale Vic $22.15
            """;

        var result = _parser.Parse("statement.pdf", Text);

        Assert.Empty(result.Issues);
        Assert.Equal([35.02m, 22.15m], result.Rows.Select(r => r.Cost));
    }

    [Fact]
    public void Parse_ReportsIssue_WhenAWrappedTransactionNeverGetsItsAmount()
    {
        const string Text = """
            Date Card Description Debits Credits
            04/07/2026 1234 Anthropic* Claude Sub San Francisco Ca
            34.00 AUD Rate:1.000000
            05/07/2026 1234 Kmart 1262 Broadmeadows Vic $114.45
            06/07/2026 1234 Some Overseas Merchant
            Closing balance $1,000.00
            $50.00
            """;

        var result = _parser.Parse("statement.pdf", Text);

        // Neither the next transaction's amount, the closing balance nor a stray
        // amount after it is taken as the missing amount.
        var row = Assert.Single(result.Rows);
        Assert.Equal((114.45m, "Kmart 1262 Broadmeadows Vic"), (row.Cost, row.Description));
        Assert.Collection(
            result.Issues,
            issue => Assert.Equal("Anthropic* Claude Sub San Francisco Ca", issue.Description),
            issue => Assert.Equal("Some Overseas Merchant", issue.Description));
        Assert.All(result.Issues, issue => Assert.Contains("has no amount", issue.Error));
    }
}
