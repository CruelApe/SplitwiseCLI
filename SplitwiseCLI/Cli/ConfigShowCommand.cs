using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Configuration;

namespace SplitwiseCLI.Cli;

public sealed class ConfigShowCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        var store = new UserConfigStore();
        var apiKey = store.LoadApiKey();
        var envKey = EnvConfigLoader.TryGetApiKeyFromEnvironment();

        AnsiConsole.MarkupLine($"Config file: [grey]{store.ConfigFilePath.EscapeMarkup()}[/]");

        AnsiConsole.MarkupLine(apiKey is null
            ? "Saved API key: [red]not set[/]"
            : $"Saved API key: [green]set[/] (ends with ...{Mask(apiKey)})");

        AnsiConsole.MarkupLine(envKey is null
            ? "SPLITWISE_API_KEY env var: [grey]not set[/]"
            : "SPLITWISE_API_KEY env var: [green]set[/] (takes precedence over the saved key)");

        return 0;
    }

    private static string Mask(string apiKey) => apiKey.Length <= 4 ? apiKey : apiKey[^4..];
}
