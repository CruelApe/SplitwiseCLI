using DotNetEnv;

namespace SplitwiseCLI.Configuration;

public sealed class ConfigurationException(string message) : Exception(message);

public static class EnvConfigLoader
{
    public const string ApiBaseUrl = "https://secure.splitwise.com/api/v3.0";

    public static void LoadDotEnvIfPresent()
    {
        var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
        }
        else
        {
            // Also check upward from the current working directory, since a globally
            // installed tool is invoked from arbitrary directories, not the project folder.
            Env.TraversePath().Load();
        }
    }

    public static string? TryGetApiKeyFromEnvironment() => NullIfWhiteSpace(Environment.GetEnvironmentVariable("SPLITWISE_API_KEY"));

    public static string? TryGetDefaultCurrencyFromEnvironment() => NullIfWhiteSpace(Environment.GetEnvironmentVariable("SPLITWISE_DEFAULT_CURRENCY"));

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
