using Spectre.Console;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Output;

public static class ExpenseRenderer
{
    public static void RenderList(IReadOnlyList<Expense> expenses)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Id");
        table.AddColumn("Date");
        table.AddColumn("Description");
        table.AddColumn("Cost");
        table.AddColumn("Group");

        foreach (var expense in expenses.OrderByDescending(e => e.Date))
        {
            table.AddRow(
                expense.Id.ToString(),
                expense.Date?.ToString("yyyy-MM-dd") ?? "-",
                (expense.Description ?? "-").EscapeMarkup(),
                $"{expense.Cost} {expense.CurrencyCode}".Trim().EscapeMarkup(),
                expense.GroupId is null or 0 ? "-" : expense.GroupId.ToString()!);
        }

        AnsiConsole.Write(table);
    }

    public static void RenderDetail(Expense expense)
    {
        var summary = new Table().Border(TableBorder.Rounded).HideHeaders();
        summary.AddColumn("Field");
        summary.AddColumn("Value");

        summary.AddRow("Id", expense.Id.ToString());
        summary.AddRow("Description", (expense.Description ?? "-").EscapeMarkup());
        summary.AddRow("Cost", $"{expense.Cost} {expense.CurrencyCode}".Trim().EscapeMarkup());
        summary.AddRow("Date", expense.Date?.ToString("yyyy-MM-dd") ?? "-");
        summary.AddRow("Category", expense.Category is null ? "-" : $"{expense.Category.Name} (id: {expense.Category.Id})".EscapeMarkup());
        summary.AddRow("Group id", expense.GroupId is null or 0 ? "-" : expense.GroupId.ToString()!);
        summary.AddRow("Deleted", expense.Deleted == true ? "yes" : "no");

        AnsiConsole.Write(summary);

        if (expense.Users.Count > 0)
        {
            var shares = new Table().Border(TableBorder.Rounded);
            shares.AddColumn("User");
            shares.AddColumn("Paid");
            shares.AddColumn("Owed");
            shares.AddColumn("Net balance");

            foreach (var share in expense.Users)
            {
                shares.AddRow(
                    $"{share.User.FirstName} {share.User.LastName}".Trim().EscapeMarkup(),
                    share.PaidShare ?? "-",
                    share.OwedShare ?? "-",
                    share.NetBalance ?? "-");
            }

            AnsiConsole.Write(shares);
        }
    }
}
