using System.Globalization;
using System.Text.RegularExpressions;
using SplitwiseCLI.Services;

namespace SplitwiseCLI.Statements;

public sealed partial class NabClassicBankingStatementParser : IStatementParser
{
    private static readonly string[] InternalTransferMarkers = ["Latitude Go Internet Bpay", "Coles Mastercard Internet Bpay"];

    public string InstitutionName => "NAB Classic Banking";

    [GeneratedRegex(
        @"^(?<date>\d{1,2}\s+[A-Za-z]{3}(?:\s+\d{4})?)\s+(?<desc>.+?)\s+(?<debit>\d{1,3}(?:,\d{3})*\.\d{2})?\s+(?<credit>\d{1,3}(?:,\d{3})*\.\d{2})?\s+(?<balance>\d{1,3}(?:,\d{3})*\.\d{2}\s*Cr)?$")]
    private static partial Regex LineRegex();

    public bool CanParse(string text) =>
        StatementTextUtils.HasHeaderLine(text, "Date", "Particulars", "Debits", "Credits", "Balance");

    public IReadOnlyList<MergedExpenseRow> Parse(string sourceFile, string text)
    {
        var rows = new List<MergedExpenseRow>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            var match = LineRegex().Match(line);
            var debitGroup = match.Groups["debit"];
            if (!match.Success || !debitGroup.Success)
            {
                continue;
            }

            var description = match.Groups["desc"].Value.Trim();

            // Internal transfers that fund the credit cards would double-count
            // the same spend already captured on those cards' own statements.
            if (InternalTransferMarkers.Any(marker => description.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!TryParseNabDate(match.Groups["date"].Value.Trim(), out var date))
            {
                continue;
            }

            var cost = decimal.Parse(debitGroup.Value, NumberStyles.Number, CultureInfo.InvariantCulture);
            rows.Add(new MergedExpenseRow(sourceFile, description, cost, date, null, null, null));
        }

        return rows;
    }

    // NAB statements sometimes omit the year on each line (relying on the
    // statement period for context) - fall back to the current year, which is
    // wrong for a statement spanning a year boundary (a known limitation).
    private static bool TryParseNabDate(string rawDate, out DateTime date)
    {
        if (DateTime.TryParseExact(rawDate, "d MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        return DateTime.TryParseExact($"{rawDate} {DateTime.Now.Year}", "d MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
