using SplitwiseCLI.Statements;
using Xunit;

namespace SplitwiseCLI.Tests;

// Statement text below mirrors what PdfPig actually extracts from a NAB Classic
// Banking statement: transactions split over two lines, dot leaders up to the
// amount, the date only on a day's first transaction, and the day's closing
// balance glued onto its last amount ("18.34 Cr342.15").
public class NabClassicBankingStatementParserTests
{
    private readonly NabClassicBankingStatementParser _parser = new();

    [Fact]
    public void CanParse_TrueForMatchingHeader_FalseOtherwise()
    {
        const string Header = "Date Particulars Debits Credits Balance";
        Assert.True(_parser.CanParse(Header));
        Assert.False(_parser.CanParse("Processed Date Transaction Date Details Amount"));
    }

    [Fact]
    public void Parse_JoinsTwoLineTransactions_AndUsesTheCardPurchaseDate()
    {
        const string Text = """
            Date Particulars Debits Credits Balance
            4 Jul 2026 Brought forward 500.00 Cr
            6 Jul 2026 abc123 Telstra Services
            654321.................................................................. 129.52
            V1234 04/07 Google Chap Chore Tra Bara
            Ref: 11111111111.................................................... 9.99
            V1234 03/07 Wings Melbourne Pty Ltd Sout
            Ref: 22222222222........................................................ 18.34 Cr342.15
            7 Jul 2026 EFTPOS 07/07 03:29 Easypark.................................. 16.06 Cr326.09
            """;

        var result = _parser.Parse("statement.pdf", Text);

        Assert.Empty(result.Issues);
        Assert.Equal(
            [
                ("abc123 Telstra Services", 129.52m, new DateTime(2026, 7, 6)),
                ("Google Chap Chore Tra Bara", 9.99m, new DateTime(2026, 7, 4)),
                ("Wings Melbourne Pty Ltd Sout", 18.34m, new DateTime(2026, 7, 3)),
                ("Easypark", 16.06m, new DateTime(2026, 7, 7)),
            ],
            result.Rows.Select(r => (r.Description, r.Cost, r.Date)));
        Assert.All(result.Rows, r => Assert.Null(r.CategoryId));
        Assert.All(result.Rows, r => Assert.Null(r.GroupId));
    }

    [Fact]
    public void Parse_UsesTheRunningBalance_ToLeaveOutCredits()
    {
        const string Text = """
            Date Particulars Debits Credits Balance
            4 Jul 2026 Brought forward 100.00 Cr
            8 Jul 2026 J Citizen............................................................................ 300.00
            100000001-20000000 Linkt Melbourne
            123456.............................................................................. 3.37 Cr396.63
            14 Jul 2026 Salary Example Pty Ltd
            987654.............................................................................. 5,000.00
            100000002-20000000 Linkt Melbourne
            123456.............................................................................. 10.95 Cr5,385.68
            """;

        var result = _parser.Parse("statement.pdf", Text);

        Assert.Empty(result.Issues);
        Assert.Equal(
            [("100000001-20000000 Linkt Melbourne", 3.37m), ("100000002-20000000 Linkt Melbourne", 10.95m)],
            result.Rows.Select(r => (r.Description, r.Cost)));
    }

    [Fact]
    public void Parse_ExcludesInternalCardFundingTransfers_ButCountsThemTowardsTotalDebits()
    {
        const string Text = """
            Total debits $2,322.50
            Date Particulars Debits Credits Balance
            15 Jul 2026 Brought forward 3,000.00 Cr
            16 Jul 2026 Internet Bpay Coles Mastercard
            4202000000000000............................................................ 300.00
            Internet Bpay Latitude Go
            5218000000000000............................................................ 2,000.00
            V1234 15/07 Gyg Roxburgh Park Roxb
            Ref: 33333333333.............................................................. 22.50 Cr677.50
            """;

        var result = _parser.Parse("statement.pdf", Text);

        Assert.Empty(result.Issues);
        var row = Assert.Single(result.Rows);
        Assert.Equal(("Gyg Roxburgh Park Roxb", 22.50m), (row.Description, row.Cost));
    }

    [Fact]
    public void Parse_JoinsADaySplitAcrossAPageBreak_WithoutPickingUpPageNoise()
    {
        const string Text = """
            Date Particulars Debits Credits Balance
            23 Jul 2026 Brought forward 1,000.00 Cr
            24 Jul 2026 V1234 23/07 Amazon Marketplace Au Sydn
            Ref: 44444444444.................................................................. 27.97
            Carried forward
            2,000.00 Cr
            972.03 Cr
            Account Details
            NAB Classic Banking
            Statement number 1 Page 3 of 5
            S-00001
            00001
            Transaction Details (continued)
            Date Particulars Debits Credits Balance
            Brought forward
            V1234 22/07 Amazon Marketplace Au Sydn
            Ref: 55555555555.................................................................. 29.99
            Glenroy Star Pizza 17/7......................................................... 59.00 Cr883.04
            """;

        var result = _parser.Parse("statement.pdf", Text);

        Assert.Empty(result.Issues);
        Assert.Equal(
            [("Amazon Marketplace Au Sydn", 27.97m), ("Amazon Marketplace Au Sydn", 29.99m), ("Glenroy Star Pizza 17/7", 59.00m)],
            result.Rows.Select(r => (r.Description, r.Cost)));
    }

    [Fact]
    public void Parse_JoinsATransactionSplitAcrossAPageBreak()
    {
        const string Text = """
            Date Particulars Debits Credits Balance
            23 Jul 2026 Brought forward 100.00 Cr
            24 Jul 2026 V1234 23/07 Amazon Marketplace Au Sydn
            Carried forward
            100.00 Cr
            Statement number 1 Page 3 of 5
            Transaction Details (continued)
            Date Particulars Debits Credits Balance
            Brought forward
            Ref: 44444444444.................................................................. 27.97 Cr72.03
            """;

        var result = _parser.Parse("statement.pdf", Text);

        Assert.Empty(result.Issues);
        var row = Assert.Single(result.Rows);
        Assert.Equal(("Amazon Marketplace Au Sydn", 27.97m, new DateTime(2026, 7, 23)), (row.Description, row.Cost, row.Date));
    }

    [Fact]
    public void Parse_ReportsIssue_AndKeepsRowsAsDebits_WhenADayDoesNotMatchTheRunningBalance()
    {
        const string Text = """
            Date Particulars Debits Credits Balance
            4 Jul 2026 Brought forward 500.00 Cr
            6 Jul 2026 V1234 04/07 Google Chap Chore Tra Bara
            Ref: 11111111111.................................................... 9.99 Cr200.00
            """;

        var result = _parser.Parse("statement.pdf", Text);

        var row = Assert.Single(result.Rows);
        Assert.Equal(9.99m, row.Cost);
        var issue = Assert.Single(result.Issues);
        Assert.Contains("don't add up to the statement's running balance", issue.Error);
    }

    [Fact]
    public void Parse_ReportsIssue_WhenTotalDebitsReadDifferFromTheStatementTotal()
    {
        const string Text = """
            Total debits $500.00
            Date Particulars Debits Credits Balance
            4 Jul 2026 Brought forward 500.00 Cr
            6 Jul 2026 V1234 04/07 Google Chap Chore Tra Bara
            Ref: 11111111111.................................................... 9.99 Cr490.01
            """;

        var result = _parser.Parse("statement.pdf", Text);

        Assert.Single(result.Rows);
        var issue = Assert.Single(result.Issues);
        Assert.Contains("$500.00", issue.Error);
        Assert.Contains("$9.99", issue.Error);
    }

    [Fact]
    public void Parse_AssumesCurrentYear_WhenDateOmitsIt()
    {
        const string Text = """
            Date Particulars Debits Credits Balance
            4 Jul Brought forward 4,020.00 Cr
            5 Jul Woolworths Supermarket.......................................... 20.00 Cr4,000.00
            """;

        var result = _parser.Parse("statement.pdf", Text);

        Assert.Empty(result.Issues);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Woolworths Supermarket", row.Description);
        Assert.Equal(new DateTime(DateTime.Now.Year, 7, 5), row.Date);
    }

    [Fact]
    public void Parse_PutsADecemberPurchasePostedInJanuary_InThePreviousYear()
    {
        const string Text = """
            Date Particulars Debits Credits Balance
            1 Jan 2027 Brought forward 100.00 Cr
            2 Jan 2027 V1234 30/12 Amazon Marketplace Au Sydn
            Ref: 44444444444.................................................................. 27.97 Cr72.03
            """;

        var result = _parser.Parse("statement.pdf", Text);

        var row = Assert.Single(result.Rows);
        Assert.Equal(new DateTime(2026, 12, 30), row.Date);
    }
}
