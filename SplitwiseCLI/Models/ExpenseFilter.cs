namespace SplitwiseCLI.Models;

// Query parameters for GET /get_expenses. Not a wire DTO - just the request-side options.
public sealed record ExpenseFilter(
    long? GroupId = null,
    long? FriendId = null,
    DateTimeOffset? DatedAfter = null,
    DateTimeOffset? DatedBefore = null,
    DateTimeOffset? UpdatedAfter = null,
    DateTimeOffset? UpdatedBefore = null,
    int? Limit = null,
    int? Offset = null);
