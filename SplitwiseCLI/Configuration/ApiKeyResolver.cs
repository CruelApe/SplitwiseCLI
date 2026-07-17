using Spectre.Console;

namespace SplitwiseCLI.Configuration;

// Resolves the Splitwise API key from, in priority order: the SPLITWISE_API_KEY env
// var, a .env file, or the app's own persisted user config - prompting interactively
// and saving to that config the first time none of those have it.
public static class ApiKeyResolver
{
    public static string Resolve(UserConfigStore userConfigStore)
    {
        EnvConfigLoader.LoadDotEnvIfPresent();

        var apiKey = EnvConfigLoader.TryGetApiKeyFromEnvironment() ?? userConfigStore.LoadApiKey();
        if (apiKey is not null)
        {
            return apiKey;
        }

        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            throw new ConfigurationException(
                "SPLITWISE_API_KEY is not set and no saved API key was found. Set the SPLITWISE_API_KEY " +
                "environment variable, create a .env file (see .env.example), or run 'splitwise config set-key' " +
                "interactively.");
        }

        AnsiConsole.MarkupLine("[yellow]No Splitwise API key is configured yet.[/]");
        AnsiConsole.MarkupLine(
            "Get a personal API key from your Splitwise account/apps (https://secure.splitwise.com/apps), then paste it below.");
        var entered = AnsiConsole.Prompt(new TextPrompt<string>("API key:").Secret());

        userConfigStore.SaveApiKey(entered);
        AnsiConsole.MarkupLine($"[green]Saved.[/] Stored (encrypted) at [grey]{userConfigStore.ConfigFilePath.EscapeMarkup()}[/] for next time.");

        return entered;
    }
}
