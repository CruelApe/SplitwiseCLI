using System.Text.Json.Serialization;

namespace SplitwiseCLI.Models;

// Also used for GET /get_user/{id}, which wraps a single user the same way.
public sealed class CurrentUserResponse
{
    [JsonPropertyName("user")]
    public User User { get; init; } = new();
}

public sealed class User
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("locale")]
    public string? Locale { get; init; }

    [JsonPropertyName("default_currency")]
    public string? DefaultCurrency { get; init; }
}
