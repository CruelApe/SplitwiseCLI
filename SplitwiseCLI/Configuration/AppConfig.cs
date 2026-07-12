namespace SplitwiseCLI.Configuration;

public sealed record AppConfig(string ApiKey, string ApiBaseUrl, string? DefaultCurrencyOverride);
