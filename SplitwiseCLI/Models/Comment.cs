using System.Text.Json.Serialization;

namespace SplitwiseCLI.Models;

public sealed class CommentsResponse
{
    [JsonPropertyName("comments")]
    public List<Comment> Comments { get; init; } = [];
}

public sealed class Comment
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("comment_type")]
    public string? CommentType { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; init; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; init; }

    [JsonPropertyName("user")]
    public CommentUser? User { get; init; }
}

public sealed class CommentUser
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }
}
