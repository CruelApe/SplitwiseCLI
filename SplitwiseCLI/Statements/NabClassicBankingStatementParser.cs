using System.Globalization;
using System.Text.RegularExpressions;
using SplitwiseCLI.Services;

namespace SplitwiseCLI.Statements;

public sealed partial class NabClassicBankingStatementParser : IStatementParser
{
    // Credits (salary, transfers in, refunds) are rare, so each day is resolved by the
    // fewest credits that explain its balance - a day that needs more than this is
    // reported for a manual check rather than guessed at.
    private const int MaxCreditsPerDay = 3;

    // Payments into the credit cards - that spend is already captured on the cards'
    // own statements, so merging these too would double-count it.
    private static readonly string[] InternalTransferMarkers = ["Internet Bpay Latitude Go", "Internet Bpay Coles Mastercard"];

    public string InstitutionName => "NAB Classic Banking";

    // Every transaction ends on a line whose dot leaders run up to its amount. The
    // last transaction of a day also carries the day's closing balance, which the
    // extracted text puts straight after the amount:
    //   Ref: 22222222222.................................. 18.34 Cr100.75
    [GeneratedRegex(
        @"^(?<text>.*?)\s*\.{2,}\s*(?<amount>\d{1,3}(?:,\d{3})*\.\d{2})(?:\s+(?<sign>Cr|Dr)\s*(?<balance>\d{1,3}(?:,\d{3})*\.\d{2}))?$")]
    private static partial Regex AmountLineRegex();

    [GeneratedRegex(@"^(?<date>\d{1,2}\s+[A-Za-z]{3}(?:\s+\d{4})?)\s+(?<rest>.+)$")]
    private static partial Regex DatedLineRegex();

    [GeneratedRegex(@"^(?:Opening balance\s+\$?|Brought forward\s+)(?<balance>\d{1,3}(?:,\d{3})*\.\d{2})\s*(?<sign>Cr|Dr)$")]
    private static partial Regex OpeningBalanceRegex();

    [GeneratedRegex(@"^Total debits\s+\$(?<amount>\d{1,3}(?:,\d{3})*\.\d{2})$")]
    private static partial Regex TotalDebitsRegex();

    // The second line of a two-line transaction, when it holds nothing but a reference.
    [GeneratedRegex(@"^(?:Ref:\s*)?\d*$")]
    private static partial Regex ReferenceOnlyRegex();

    // Card ("V1234 15/07") and EFTPOS ("EFTPOS 07/07 03:29") prefixes, which carry the
    // day the purchase was actually made rather than the day NAB posted it.
    [GeneratedRegex(@"^(?:V\d{4}|EFTPOS)\s+(?<day>\d{2})/(?<month>\d{2})(?:\s+\d{2}:\d{2})?\s+")]
    private static partial Regex PurchasePrefixRegex();

    public bool CanParse(string text) =>
        StatementTextUtils.HasHeaderLine(text, "Date", "Particulars", "Debits", "Credits", "Balance");

    public StatementParseResult Parse(string sourceFile, string text)
    {
        var rows = new List<MergedExpenseRow>();
        var issues = new List<MergeRowIssue>();

        // Transactions read since the last printed balance - which of them are debits
        // is only known once that day's closing balance shows up.
        var day = new List<NabTransaction>();
        decimal? balance = null;
        decimal? statementTotalDebits = null;
        decimal totalDebitsRead = 0;
        DateTime? postedDate = null;
        string? pendingText = null;
        var inTransactionTable = false;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (TotalDebitsRegex().Match(line) is { Success: true } totalMatch)
            {
                statementTotalDebits = ParseAmount(totalMatch.Groups["amount"].Value);
                continue;
            }

            // Each page's table runs from its column header down to "Carried forward" -
            // skipping the page header/footer in between lets a transaction that's
            // split across a page break still join up.
            if (line.StartsWith("Date Particulars", StringComparison.Ordinal))
            {
                inTransactionTable = true;
                continue;
            }

            if (line.StartsWith("Carried forward", StringComparison.Ordinal))
            {
                inTransactionTable = false;
                continue;
            }

            // Only the first transaction of each day shows the date - the rest inherit it.
            var dated = DatedLineRegex().Match(line);
            if (inTransactionTable && dated.Success && TryParseNabDate(dated.Groups["date"].Value, out var lineDate))
            {
                postedDate = lineDate;
                line = dated.Groups["rest"].Value;
            }

            if (OpeningBalanceRegex().Match(line) is { Success: true } openingMatch)
            {
                balance ??= ParseBalance(openingMatch);
                continue;
            }

            if (!inTransactionTable || line.StartsWith("Brought forward", StringComparison.Ordinal))
            {
                continue;
            }

            var amountMatch = AmountLineRegex().Match(line);
            if (!amountMatch.Success)
            {
                // The first line(s) of a transaction whose amount is on a later line.
                pendingText = pendingText is null ? line : $"{pendingText} {line}";
                continue;
            }

            var ownText = amountMatch.Groups["text"].Value.Trim();
            var description = ReferenceOnlyRegex().IsMatch(ownText) && pendingText is not null
                ? pendingText
                : pendingText is null ? ownText : $"{pendingText} {ownText}";
            pendingText = null;

            if (postedDate is not { } posted)
            {
                issues.Add(new MergeRowIssue(sourceFile, 0, description, "Transaction has no date before it - not merged, check the statement."));
                continue;
            }

            day.Add(new NabTransaction(
                posted, ResolvePurchaseDate(description, posted), StripPurchasePrefix(description),
                ParseAmount(amountMatch.Groups["amount"].Value)));

            if (amountMatch.Groups["balance"].Success)
            {
                var closingBalance = ParseBalance(amountMatch);
                totalDebitsRead += SettleDay(sourceFile, day, balance, closingBalance, rows, issues);
                balance = closingBalance;
                day.Clear();
            }
        }

        if (day.Count > 0)
        {
            issues.Add(new MergeRowIssue(sourceFile, 0, null,
                $"{day.Count} transaction(s) after the last printed balance couldn't be checked against it - merged them all as debits, check them against the statement."));
            totalDebitsRead += AddDebitRows(sourceFile, day, new HashSet<int>(), rows);
        }

        if (statementTotalDebits is { } expected && expected != totalDebitsRead)
        {
            issues.Add(new MergeRowIssue(sourceFile, 0, null,
                $"Statement's total debits are ${expected.ToString("N2", CultureInfo.InvariantCulture)} but " +
                $"${totalDebitsRead.ToString("N2", CultureInfo.InvariantCulture)} were read - some transactions may be missing or misread."));
        }

        return new StatementParseResult(rows, issues);
    }

    // Splits a day's transactions into debits and credits using the balances either
    // side of it, adds the debits as rows, and returns the total debited (internal
    // transfers included, to compare against the statement's own "Total debits").
    private static decimal SettleDay(
        string sourceFile, IReadOnlyList<NabTransaction> day, decimal? openingBalance, decimal closingBalance,
        List<MergedExpenseRow> rows, List<MergeRowIssue> issues)
    {
        if (openingBalance is { } opening && TryFindCredits(day, opening, closingBalance, out var credits))
        {
            return AddDebitRows(sourceFile, day, credits, rows);
        }

        issues.Add(new MergeRowIssue(sourceFile, 0, null,
            $"The {day.Count} transaction(s) posted up to {day[^1].PostedDate.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} " +
            "don't add up to the statement's running balance - merged them all as debits, check them against the statement."));
        return AddDebitRows(sourceFile, day, new HashSet<int>(), rows);
    }

    private static decimal AddDebitRows(
        string sourceFile, IReadOnlyList<NabTransaction> day, IReadOnlySet<int> creditIndexes, List<MergedExpenseRow> rows)
    {
        decimal debited = 0;
        for (var i = 0; i < day.Count; i++)
        {
            if (creditIndexes.Contains(i))
            {
                continue;
            }

            var transaction = day[i];
            debited += transaction.Amount;

            if (InternalTransferMarkers.Any(marker => transaction.Description.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            rows.Add(new MergedExpenseRow(sourceFile, transaction.Description, transaction.Amount, transaction.Date, null, null, null));
        }

        return debited;
    }

    // opening - debits + credits = closing, and debits + credits = every amount that
    // day, so the credits must sum to exactly (closing - opening + every amount) / 2.
    // Only accepted when exactly one set of the smallest size that works does so.
    private static bool TryFindCredits(
        IReadOnlyList<NabTransaction> day, decimal openingBalance, decimal closingBalance, out IReadOnlySet<int> creditIndexes)
    {
        var creditTotal = (closingBalance - openingBalance + day.Sum(t => t.Amount)) / 2;

        for (var size = 0; size <= Math.Min(day.Count, MaxCreditsPerDay); size++)
        {
            var matches = FindIndexSets(day, size, creditTotal, 0).Take(2).ToList();
            if (matches.Count == 1)
            {
                creditIndexes = matches[0];
                return true;
            }

            if (matches.Count > 1)
            {
                break;
            }
        }

        creditIndexes = new HashSet<int>();
        return false;
    }

    private static IEnumerable<HashSet<int>> FindIndexSets(IReadOnlyList<NabTransaction> day, int size, decimal total, int start)
    {
        if (size == 0)
        {
            if (total == 0)
            {
                yield return [];
            }

            yield break;
        }

        for (var i = start; i <= day.Count - size; i++)
        {
            if (day[i].Amount > total)
            {
                continue;
            }

            foreach (var rest in FindIndexSets(day, size - 1, total - day[i].Amount, i + 1))
            {
                rest.Add(i);
                yield return rest;
            }
        }
    }

    // A card/EFTPOS purchase uses the day it was made (the date Latitude and Coles rows
    // use too); anything else uses the day NAB posted it.
    private static DateTime ResolvePurchaseDate(string description, DateTime postedDate)
    {
        var prefix = PurchasePrefixRegex().Match(description);
        if (!prefix.Success ||
            !DateTime.TryParseExact($"{prefix.Groups["day"].Value}/{prefix.Groups["month"].Value}/{postedDate.Year}",
                "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var purchaseDate))
        {
            return postedDate;
        }

        // A purchase is never after the day it was posted - one that seems to be was
        // made in December and posted in January.
        return purchaseDate > postedDate ? purchaseDate.AddYears(-1) : purchaseDate;
    }

    private static string StripPurchasePrefix(string description)
    {
        var stripped = PurchasePrefixRegex().Replace(description, "").Trim();
        return stripped.Length > 0 ? stripped : description;
    }

    private static decimal ParseAmount(string value) =>
        decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

    private static decimal ParseBalance(Match match)
    {
        var value = ParseAmount(match.Groups["balance"].Value);
        return match.Groups["sign"].Value == "Dr" ? -value : value;
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

    private sealed record NabTransaction(DateTime PostedDate, DateTime Date, string Description, decimal Amount);
}
