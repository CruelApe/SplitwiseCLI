using SplitwiseCLI.Services;

namespace SplitwiseCLI.Statements;

// Issues cover anything a parser recognized as a transaction (or a statement total)
// but couldn't turn into a trustworthy row - surfacing them is what keeps a PDF
// transaction from silently going missing from the merged output.
public sealed record StatementParseResult(IReadOnlyList<MergedExpenseRow> Rows, IReadOnlyList<MergeRowIssue> Issues);
