namespace SplitwiseCLI.Services;

public sealed record ImportRowResult(
    string SourceFile,
    int RowNumber,
    string? Description,
    bool Success,
    string? ErrorMessage,
    long? ExpenseId,
    string? BatchId,
    string? Cost = null,
    string? Date = null,
    long? CategoryId = null,
    long? GroupId = null,
    string? Details = null);
