using System.Text.Json.Serialization;

namespace SplitwiseCLI.Models;

// GET /get_currencies returns a bare JSON array, unlike the other endpoints
// which wrap their payload in a named object.
public sealed class Currency
{
    [JsonPropertyName("currency_code")]
    public string? CurrencyCode { get; init; }

    [JsonPropertyName("unit")]
    public string? Unit { get; init; }
}
