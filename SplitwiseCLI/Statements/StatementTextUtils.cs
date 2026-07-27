namespace SplitwiseCLI.Statements;

internal static class StatementTextUtils
{
    // A statement's header row is used to identify its institution rather than
    // scanning the whole document for any one token - a single transaction
    // description elsewhere in the file could otherwise cause a false match.
    public static bool HasHeaderLine(string text, params string[] tokens) =>
        text.Split('\n').Any(line => tokens.All(t => line.Contains(t, StringComparison.Ordinal)));
}
