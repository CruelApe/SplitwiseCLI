using System.Text.Json.Serialization;

namespace SplitwiseCLI.Models;

public sealed class NotificationsResponse
{
    [JsonPropertyName("notifications")]
    public List<Notification> Notifications { get; init; } = [];
}

public sealed class Notification
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("type")]
    public int Type { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; init; }
}
