using SplitwiseCLI.Api;
using SplitwiseCLI.Configuration;

namespace SplitwiseCLI.Services;

public static class CurrencyResolver
{
    public static async Task<string> ResolveAsync(ISplitwiseClient client, AppConfig config, CancellationToken cancellationToken = default)
    {
        var currentUser = await client.GetCurrentUserAsync(cancellationToken);
        return config.DefaultCurrencyOverride ?? currentUser.DefaultCurrency
            ?? throw new SplitwiseApiException(
                "Could not determine a currency to use: no SPLITWISE_DEFAULT_CURRENCY override is set and the " +
                "Splitwise account has no default currency.");
    }
}
