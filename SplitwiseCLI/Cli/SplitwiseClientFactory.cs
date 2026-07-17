using SplitwiseCLI.Api;
using SplitwiseCLI.Configuration;

namespace SplitwiseCLI.Cli;

// Config/HTTP client construction is deferred until a command actually executes
// (rather than done eagerly in the composition root), so that things like
// "splitwise --help" work without an API key being configured.
public sealed class SplitwiseClientFactory
{
    public (ISplitwiseClient Client, AppConfig Config) Create()
    {
        var apiKey = ApiKeyResolver.Resolve(new UserConfigStore());
        var currencyOverride = EnvConfigLoader.TryGetDefaultCurrencyFromEnvironment();

        var config = new AppConfig(apiKey, EnvConfigLoader.ApiBaseUrl, currencyOverride);
        var httpClient = new HttpClient();
        var client = new SplitwiseClient(httpClient, config);
        return (client, config);
    }
}
