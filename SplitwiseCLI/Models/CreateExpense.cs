using System.Text.Json;
using System.Text.Json.Serialization;

namespace SplitwiseCLI.Models;

public sealed class CreateExpenseRequest
{
    [JsonPropertyName("cost")]
    public required string Cost { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("date")]
    public required string Date { get; init; }

    [JsonPropertyName("currency_code")]
    public required string CurrencyCode { get; init; }

    [JsonPropertyName("category_id")]
    public required long CategoryId { get; init; }

    [JsonPropertyName("group_id")]
    public required long GroupId { get; init; }

    [JsonPropertyName("split_equally")]
    public bool SplitEqually { get; init; } = true;
}

public sealed class CreateExpenseResponse
{
    [JsonPropertyName("success")]
    public bool? Success { get; init; }

    // Splitwise's error payload shape varies by endpoint/error type, so this is
    // kept loosely typed rather than a fixed DTO.
    [JsonPropertyName("errors")]
    public JsonElement? Errors { get; init; }

    [JsonPropertyName("expenses")]
    public List<CreatedExpense>? Expenses { get; init; }
}

public sealed class CreatedExpense
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("cost")]
    public string? Cost { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
