using System.Globalization;
using System.Text.RegularExpressions;
using SplitwiseCLI.Services;

namespace SplitwiseCLI.Statements;

public sealed partial class LatitudeGoStatementParser : IStatementParser
{
    public string InstitutionName => "Latitude Go Mastercard";

    [GeneratedRegex(
        @"^(?<date>\d{2}/\d{2}/\d{4})\s+(?<card>\d{4})\s+(?<desc>.+?)\s+(?:\$(?<debit>\d{1,3}(?:,\d{3})*\.\d{2}))?\s*(?:\$(?<credit>\d{1,3}(?:,\d{3})*\.\d{2}))?$")]
    private static partial Regex LineRegex();

    // An international purchase is printed across several lines rather than one -
    // the description first with no amount, then the foreign amount/exchange rate
    // and the international transaction fee, and only then the total on its own:
    //   04/07/2026 1234 Anthropic* Claude Sub San Francisco Ca
    //   34.00 AUD Rate:1.000000
    //   Txn Inc 1.02 International Transaction Fee
    //   $35.02
    [GeneratedRegex(@"^(?<date>\d{2}/\d{2}/\d{4})\s+(?<card>\d{4})\s+(?<desc>.+)$")]
    private static partial Regex WrappedStartRegex();

    [GeneratedRegex(@"^\$(?<debit>\d{1,3}(?:,\d{3})*\.\d{2})(?:\s+\$(?<credit>\d{1,3}(?:,\d{3})*\.\d{2}))?$")]
    private static partial Regex AmountOnlyLineRegex();

    public bool CanParse(string text) =>
        StatementTextUtils.HasHeaderLine(text, "Date", "Card", "Description", "Debits", "Credits");

    public StatementParseResult Parse(string sourceFile, string text)
    {
        var rows = new List<MergedExpenseRow>();
        var issues = new List<MergeRowIssue>();

        // A transaction line still waiting for the amount-only line that completes it.
        Match? wrapped = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();

            var match = LineRegex().Match(line);
            if (match.Success)
            {
                ReportUnfinished(sourceFile, wrapped, issues);
                wrapped = null;
                AddDebitRow(sourceFile, match.Groups["date"].Value, match.Groups["desc"].Value, match.Groups["debit"], rows, issues);
                continue;
            }

            var wrappedStart = WrappedStartRegex().Match(line);
            if (wrappedStart.Success)
            {
                ReportUnfinished(sourceFile, wrapped, issues);
                wrapped = wrappedStart;
                continue;
            }

            if (wrapped is null)
            {
                continue;
            }

            // The end of the transaction list - a wrapped transaction still open here
            // never got its amount, and nothing after this point could be it.
            if (line.StartsWith("Closing balance", StringComparison.OrdinalIgnoreCase))
            {
                ReportUnfinished(sourceFile, wrapped, issues);
                wrapped = null;
                continue;
            }

            var amount = AmountOnlyLineRegex().Match(line);
            if (amount.Success)
            {
                AddDebitRow(sourceFile, wrapped.Groups["date"].Value, wrapped.Groups["desc"].Value, amount.Groups["debit"], rows, issues);
                wrapped = null;
            }
        }

        ReportUnfinished(sourceFile, wrapped, issues);
        return new StatementParseResult(rows, issues);
    }

    private static void AddDebitRow(
        string sourceFile, string rawDate, string rawDescription, Group debitGroup,
        List<MergedExpenseRow> rows, List<MergeRowIssue> issues)
    {
        if (!debitGroup.Success)
        {
            // No debit amount on this line - a credit-only row.
            return;
        }

        var description = rawDescription.Trim();
        if (description.Contains("BPAY Payment Received", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!DateTime.TryParseExact(rawDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            issues.Add(new MergeRowIssue(sourceFile, 0, description, $"Unreadable transaction date '{rawDate}' - not merged."));
            return;
        }

        var cost = decimal.Parse(debitGroup.Value, NumberStyles.Number, CultureInfo.InvariantCulture);
        rows.Add(new MergedExpenseRow(sourceFile, description, cost, date, null, null, null));
    }

    private static void ReportUnfinished(string sourceFile, Match? wrapped, List<MergeRowIssue> issues)
    {
        if (wrapped is null)
        {
            return;
        }

        issues.Add(new MergeRowIssue(
            sourceFile, 0, wrapped.Groups["desc"].Value.Trim(),
            $"Transaction dated {wrapped.Groups["date"].Value} has no amount that could be read - not merged, check the statement."));
    }
}
