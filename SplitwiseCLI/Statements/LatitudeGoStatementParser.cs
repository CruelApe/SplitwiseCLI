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

    public bool CanParse(string text) =>
        StatementTextUtils.HasHeaderLine(text, "Date", "Card", "Description", "Debits", "Credits");

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

            var debitGroup = match.Groups["debit"];
            if (!debitGroup.Success)
            {
                // No debit amount on this line - either a credit-only row or noise.
                continue;
            }

            var description = match.Groups["desc"].Value.Trim();
            if (description.Contains("BPAY Payment Received", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!DateTime.TryParseExact(match.Groups["date"].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            var cost = decimal.Parse(debitGroup.Value, NumberStyles.Number, CultureInfo.InvariantCulture);
            rows.Add(new MergedExpenseRow(sourceFile, description, cost, date, null, null, null));
        }

        return rows;
    }
}
