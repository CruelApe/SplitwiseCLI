using System.Text.Json.Serialization;

namespace SplitwiseCLI.Models;

public sealed class GroupsResponse
{
    [JsonPropertyName("groups")]
    public List<Group> Groups { get; init; } = [];
}

public sealed class GroupResponse
{
    [JsonPropertyName("group")]
    public Group Group { get; init; } = new();
}

public sealed class Group
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("members")]
    public List<GroupMember> Members { get; init; } = [];
}

public sealed class GroupMember
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }
}
