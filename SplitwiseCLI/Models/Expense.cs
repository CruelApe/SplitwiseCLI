using System.Text.Json.Serialization;

namespace SplitwiseCLI.Models;

public sealed class ExpensesResponse
{
    [JsonPropertyName("expenses")]
    public List<Expense> Expenses { get; init; } = [];
}

public sealed class ExpenseResponse
{
    [JsonPropertyName("expense")]
    public Expense Expense { get; init; } = new();
}

public sealed class Expense
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("cost")]
    public string? Cost { get; init; }

    [JsonPropertyName("currency_code")]
    public string? CurrencyCode { get; init; }

    [JsonPropertyName("date")]
    public DateTimeOffset? Date { get; init; }

    // GET responses nest category as {"category": {"id":..., "name":...}} rather than
    // a flat "category_id" (unlike the create_expense request, which does use category_id).
    [JsonPropertyName("category")]
    public ExpenseCategoryRef? Category { get; init; }

    [JsonPropertyName("group_id")]
    public long? GroupId { get; init; }

    [JsonPropertyName("deleted")]
    public bool? Deleted { get; init; }

    [JsonPropertyName("payment")]
    public bool? Payment { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; init; }

    [JsonPropertyName("users")]
    public List<ExpenseUserShare> Users { get; init; } = [];

    [JsonPropertyName("repayments")]
    public List<Repayment> Repayments { get; init; } = [];
}

public sealed class ExpenseCategoryRef
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

public sealed class ExpenseUserShare
{
    [JsonPropertyName("user")]
    public User User { get; init; } = new();

    [JsonPropertyName("paid_share")]
    public string? PaidShare { get; init; }

    [JsonPropertyName("owed_share")]
    public string? OwedShare { get; init; }

    [JsonPropertyName("net_balance")]
    public string? NetBalance { get; init; }
}

public sealed class Repayment
{
    [JsonPropertyName("from")]
    public long From { get; init; }

    [JsonPropertyName("to")]
    public long To { get; init; }

    [JsonPropertyName("amount")]
    public string? Amount { get; init; }
}
