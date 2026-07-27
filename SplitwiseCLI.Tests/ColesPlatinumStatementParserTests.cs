using SplitwiseCLI.Statements;
using Xunit;

namespace SplitwiseCLI.Tests;

public class ColesPlatinumStatementParserTests
{
    private readonly ColesPlatinumStatementParser _parser = new();

    [Fact]
    public void CanParse_TrueForMatchingHeader_FalseOtherwise()
    {
        const string Header = "Processed Date Transaction Date Details Amount";
        Assert.True(_parser.CanParse(Header));
        Assert.False(_parser.CanParse("Date Card Description Debits Credits"));
    }

    [Fact]
    public void Parse_ExtractsDebitEntry_UsingTransactionDate()
    {
        const string Text = """
            Processed Date Transaction Date Details Amount
            02/05/26 01/05/26 Coles Supermarket Greenvale $45.67 Dr
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
    public void Parse_IgnoresCreditEntries()
    {
        const string Text = """
            Processed Date Transaction Date Details Amount
            03/05/26 02/05/26 BPAY Payment Received $200.00 Cr
            """;

        var rows = _parser.Parse("statement.pdf", Text);

        Assert.Empty(rows);
    }
}
