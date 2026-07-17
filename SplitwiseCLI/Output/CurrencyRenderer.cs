using Spectre.Console;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Output;

public static class CurrencyRenderer
{
    public static void RenderList(IReadOnlyList<Currency> currencies)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Code");
        table.AddColumn("Unit");

        foreach (var currency in currencies.OrderBy(c => c.CurrencyCode, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow((currency.CurrencyCode ?? "-").EscapeMarkup(), (currency.Unit ?? "-").EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }
}
