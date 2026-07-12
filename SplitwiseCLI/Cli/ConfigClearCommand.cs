using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Configuration;

namespace SplitwiseCLI.Cli;

public sealed class ConfigClearCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        new UserConfigStore().Clear();
        AnsiConsole.MarkupLine("[green]Cleared[/] the saved Splitwise API key.");
        return 0;
    }
}
