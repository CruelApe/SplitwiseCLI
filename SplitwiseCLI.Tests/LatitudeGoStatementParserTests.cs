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

        var rows = _parser.Parse("statement.pdf", Text);

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

        var rows = _parser.Parse("statement.pdf", Text);

        Assert.Empty(rows);
    }

    [Fact]
    public void Parse_HandlesThousandsSeparatorInAmount()
    {
        const string Text = """
            Date Card Description Debits Credits
            04/05/2026 1234 Furniture Store $1,234.56
            """;

        var rows = _parser.Parse("statement.pdf", Text);

        var row = Assert.Single(rows);
        Assert.Equal(1234.56m, row.Cost);
    }
}
