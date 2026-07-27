using SplitwiseCLI.Services;

namespace SplitwiseCLI.Statements;

public interface IStatementParser
{
    string InstitutionName { get; }

    // Detects institution by presence of that statement's distinctive header
    // tokens in the extracted text - not a full parse, just a quick match.
    bool CanParse(string text);

    // CategoryId/GroupId on every returned row are always null - PDF-derived
    // rows are left blank for the user to review and fill in by hand.
    IReadOnlyList<MergedExpenseRow> Parse(string sourceFile, string text);
}
