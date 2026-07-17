using Spectre.Console;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Output;

public static class UserRenderer
{
    public static void Render(User user)
    {
        var table = new Table().Border(TableBorder.Rounded).HideHeaders();
        table.AddColumn("Field");
        table.AddColumn("Value");

        table.AddRow("Id", user.Id.ToString());
        table.AddRow("Name", $"{user.FirstName} {user.LastName}".Trim().EscapeMarkup());
        table.AddRow("Email", (user.Email ?? "-").EscapeMarkup());
        table.AddRow("Locale", (user.Locale ?? "-").EscapeMarkup());
        table.AddRow("Default currency", (user.DefaultCurrency ?? "-").EscapeMarkup());

        AnsiConsole.Write(table);
    }
}
