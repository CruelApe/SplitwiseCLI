namespace SplitwiseCLI.Services;

public sealed record RollbackRowResult(long ExpenseId, string? Description, bool Success, string? ErrorMessage);
