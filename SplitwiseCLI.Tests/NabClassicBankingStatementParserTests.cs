using SplitwiseCLI.Statements;
using Xunit;

namespace SplitwiseCLI.Tests;

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
    public void Parse_ExtractsDebitRow_WithExplicitYear()
    {
        const string Text = """
            Date Particulars Debits Credits Balance
            15 Jul 2026 Woolworths Supermarket 87.50   4,512.10 Cr
            """;

        var rows = _parser.Parse("statement.pdf", Text);

        var row = Assert.Single(rows);
        Assert.Equal("Woolworths Supermarket", row.Description);
        Assert.Equal(87.50m, row.Cost);
        Assert.Equal(new DateTime(2026, 7, 15), row.Date);
        Assert.Null(row.CategoryId);
        Assert.Null(row.GroupId);
    }

    [Fact]
    public void Parse_AssumesCurrentYear_WhenDateOmitsIt()
    {
        const string Text = """
            Date Particulars Debits Credits Balance
            5 Jul Woolworths Supermarket 20.00   4,000.00 Cr
            """;

        var rows = _parser.Parse("statement.pdf", Text);

        var row = Assert.Single(rows);
        Assert.Equal(DateTime.Now.Year, row.Date.Year);
        Assert.Equal(7, row.Date.Month);
        Assert.Equal(5, row.Date.Day);
    }

    [Fact]
    public void Parse_ExcludesInternalCardFundingTransfers()
    {
        const string Text = """
            Date Particulars Debits Credits Balance
            10 Jul 2026 Latitude Go Internet Bpay 300.00   4,200.00 Cr
            11 Jul 2026 Coles Mastercard Internet Bpay 150.00   4,050.00 Cr
            """;

        var rows = _parser.Parse("statement.pdf", Text);

        Assert.Empty(rows);
    }

    // Known, accepted limitation (not a regression to fix): flat extracted text has
    // no column x-coordinates, so a line with only one amount plus the running
    // balance is structurally identical whether that amount is really a debit or a
    // credit - the parser has no reliable way to tell a credit-only line (income,
    // refund) from a debit-only line (an actual expense) and currently treats it as
    // a debit either way. Reviewing the merged output's Description column before
    // importing is how a stray income line like this gets caught.
    [Fact]
    public void Parse_KnownLimitation_CreditOnlyLineWithNoSeparateDebitValue_IsTreatedAsADebit()
    {
        const string Text = """
            Date Particulars Debits Credits Balance
            12 Jul 2026 Salary   2,500.00   6,550.00 Cr
            """;

        var rows = _parser.Parse("statement.pdf", Text);

        var row = Assert.Single(rows);
        Assert.Equal("Salary", row.Description);
        Assert.Equal(2500.00m, row.Cost);
    }
}
