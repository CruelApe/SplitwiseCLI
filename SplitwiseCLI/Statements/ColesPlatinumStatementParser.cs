using System.Globalization;
using System.Text.RegularExpressions;
using SplitwiseCLI.Services;

namespace SplitwiseCLI.Statements;

public sealed partial class ColesPlatinumStatementParser : IStatementParser
{
    public string InstitutionName => "Coles Platinum Mastercard";

    [GeneratedRegex(
        @"^(?<procDate>\d{2}/\d{2}/\d{2})\s+(?<txnDate>\d{2}/\d{2}/\d{2})\s+(?<desc>.+?)\s+\$(?<amount>\d{1,3}(?:,\d{3})*\.\d{2})\s*(?<type>Dr|Cr)$")]
    private static partial Regex LineRegex();

    public bool CanParse(string text) =>
        StatementTextUtils.HasHeaderLine(text, "Processed Date", "Transaction Date", "Details", "Amount");

    public IReadOnlyList<MergedExpenseRow> Parse(string sourceFile, string text)
    {
        var rows = new List<MergedExpenseRow>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            var match = LineRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            // Only "Dr" (debit) entries are expenses - "Cr" covers credit
            // adjustments and BPAY payment credits, neither of which is spending.
            if (!string.Equals(match.Groups["type"].Value, "Dr", StringComparison.Ordinal))
            {
                continue;
            }

            if (!DateTime.TryParseExact(match.Groups["txnDate"].Value, "dd/MM/yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            var description = match.Groups["desc"].Value.Trim();
            var cost = decimal.Parse(match.Groups["amount"].Value, NumberStyles.Number, CultureInfo.InvariantCulture);
            rows.Add(new MergedExpenseRow(sourceFile, description, cost, date, null, null, null));
        }

        return rows;
    }
}
