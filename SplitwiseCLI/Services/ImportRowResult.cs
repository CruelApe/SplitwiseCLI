namespace SplitwiseCLI.Services;

public sealed record ImportRowResult(
    string SourceFile,
    int RowNumber,
    string? Description,
    bool Success,
    string? ErrorMessage,
    long? ExpenseId);
