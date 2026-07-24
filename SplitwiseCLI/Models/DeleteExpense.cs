using System.Text.Json;
using System.Text.Json.Serialization;

namespace SplitwiseCLI.Models;

public sealed class DeleteExpenseResponse
{
    [JsonPropertyName("success")]
    public bool? Success { get; init; }

    // Splitwise's error payload shape varies by endpoint/error type, so this is
    // kept loosely typed rather than a fixed DTO.
    [JsonPropertyName("errors")]
    public JsonElement? Errors { get; init; }
}
