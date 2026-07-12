using Spectre.Console;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Output;

public static class FriendRenderer
{
    public static void RenderList(IReadOnlyList<Friend> friends)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Id");
        table.AddColumn("Name");
        table.AddColumn("Email");
        table.AddColumn("Balance");

        foreach (var friend in friends.OrderBy(f => f.FirstName, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(
                friend.Id.ToString(),
                $"{friend.FirstName} {friend.LastName}".Trim().EscapeMarkup(),
                (friend.Email ?? "-").EscapeMarkup(),
                FormatBalance(friend.Balance));
        }

        AnsiConsole.Write(table);
    }

    public static void RenderDetail(Friend friend)
    {
        var table = new Table().Border(TableBorder.Rounded).HideHeaders();
        table.AddColumn("Field");
        table.AddColumn("Value");

        table.AddRow("Id", friend.Id.ToString());
        table.AddRow("Name", $"{friend.FirstName} {friend.LastName}".Trim().EscapeMarkup());
        table.AddRow("Email", (friend.Email ?? "-").EscapeMarkup());
        table.AddRow("Balance", FormatBalance(friend.Balance));

        AnsiConsole.Write(table);
    }

    private static string FormatBalance(IReadOnlyList<FriendBalance> balance)
    {
        if (balance.Count == 0)
        {
            return "settled up";
        }

        return string.Join(", ", balance.Select(b => $"{b.Amount} {b.CurrencyCode}".Trim())).EscapeMarkup();
    }
}
