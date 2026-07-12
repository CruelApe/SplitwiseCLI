using System.Text.Json.Serialization;

namespace SplitwiseCLI.Models;

public sealed class FriendsResponse
{
    [JsonPropertyName("friends")]
    public List<Friend> Friends { get; init; } = [];
}

public sealed class FriendResponse
{
    [JsonPropertyName("friend")]
    public Friend Friend { get; init; } = new();
}

public sealed class Friend
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("balance")]
    public List<FriendBalance> Balance { get; init; } = [];
}

public sealed class FriendBalance
{
    [JsonPropertyName("currency_code")]
    public string? CurrencyCode { get; init; }

    [JsonPropertyName("amount")]
    public string? Amount { get; init; }
}
