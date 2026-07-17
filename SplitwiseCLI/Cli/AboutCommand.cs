using Spectre.Console;
using Spectre.Console.Cli;

namespace SplitwiseCLI.Cli;

public sealed class AboutCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        var table = new Table().Border(TableBorder.Rounded).HideHeaders();
        table.AddColumn("Field");
        table.AddColumn("Value");

        table.AddRow("Application", "SplitwiseCLI");
        table.AddRow("Version", AppInfo.Version.EscapeMarkup());
        table.AddRow("Author", AppInfo.Author.EscapeMarkup());
        table.AddRow("GitHub", AppInfo.RepositoryUrl.EscapeMarkup());

        AnsiConsole.Write(table);
        return 0;
    }
}
