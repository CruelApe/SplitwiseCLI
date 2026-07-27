using SplitwiseCLI.Statements;
using Xunit;

namespace SplitwiseCLI.Tests;

public class StatementParserRegistryTests
{
    private readonly StatementParserRegistry _registry = new();

    [Fact]
    public void Parse_SelectsColesParser_ForColesHeader()
    {
        const string Text = """
            Processed Date Transaction Date Details Amount
            02/05/26 01/05/26 Coles Supermarket $10.00 Dr
            """;

        var rows = _registry.Parse("statement.pdf", Text);

        Assert.Single(rows);
    }

    [Fact]
    public void Parse_Throws_ForUnrecognizedText()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => _registry.Parse("statement.pdf", "not a statement"));
        Assert.Contains("Unrecognized statement format", ex.Message);
    }
}
