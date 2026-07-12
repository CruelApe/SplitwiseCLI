using SplitwiseCLI.Cli;
using Xunit;

namespace SplitwiseCLI.Tests;

public class CommandLineTokenizerTests
{
    [Fact]
    public void Tokenize_SplitsOnWhitespace()
    {
        var tokens = CommandLineTokenizer.Tokenize("import path.xlsx");

        Assert.Equal(["import", "path.xlsx"], tokens);
    }

    [Fact]
    public void Tokenize_KeepsQuotedSegmentAsOneToken()
    {
        var tokens = CommandLineTokenizer.Tokenize("import \"C:/My Expenses/*.xlsx\"");

        Assert.Equal(["import", "C:/My Expenses/*.xlsx"], tokens);
    }

    [Fact]
    public void Tokenize_CollapsesRepeatedWhitespace()
    {
        var tokens = CommandLineTokenizer.Tokenize("categories   --help");

        Assert.Equal(["categories", "--help"], tokens);
    }

    [Fact]
    public void Tokenize_EmptyLine_ReturnsNoTokens()
    {
        var tokens = CommandLineTokenizer.Tokenize("   ");

        Assert.Empty(tokens);
    }
}
