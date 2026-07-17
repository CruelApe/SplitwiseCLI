using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Configuration;

namespace SplitwiseCLI.Cli;

public sealed class ConfigSetKeyCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        var apiKey = AnsiConsole.Prompt(new TextPrompt<string>("Splitwise API key:").Secret());
        var store = new UserConfigStore();
        store.SaveApiKey(apiKey);

        AnsiConsole.MarkupLine($"[green]Saved.[/] Stored (encrypted) at [grey]{store.ConfigFilePath.EscapeMarkup()}[/].");
        return 0;
    }
}
