using SplitwiseCLI.Services;

namespace SplitwiseCLI.Statements;

public sealed class StatementParserRegistry
{
    private readonly IReadOnlyList<IStatementParser> _parsers;

    public StatementParserRegistry()
        : this([new LatitudeGoStatementParser(), new ColesPlatinumStatementParser(), new NabClassicBankingStatementParser()])
    {
    }

    public StatementParserRegistry(IReadOnlyList<IStatementParser> parsers) => _parsers = parsers;

    public IReadOnlyList<MergedExpenseRow> Parse(string sourceFile, string text)
    {
        var parser = _parsers.FirstOrDefault(p => p.CanParse(text))
            ?? throw new InvalidOperationException("Unrecognized statement format - no institution parser matched.");

        return parser.Parse(sourceFile, text);
    }
}
