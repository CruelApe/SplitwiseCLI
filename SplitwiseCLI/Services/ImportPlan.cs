using SplitwiseCLI.Models;

namespace SplitwiseCLI.Services;

// A fully validated/mapped row, built without ever calling CreateExpenseAsync -
// Request is null only when the row failed local validation or mapping (unknown
// category/group etc.); a non-null Request is ready to send as-is.
public sealed record ImportPlanRow(
    string SourceFile,
    int RowNumber,
    string? Description,
    CreateExpenseRequest? Request,
    string? Error,
    string? BatchId,
    string? DuplicateReason = null);

public sealed record ImportPlan(IReadOnlyList<ImportPlanRow> Rows)
{
    // True only when there's at least one row and every single one mapped cleanly -
    // this is what gates the "proceed?" confirmation prompt in ImportCommand.
    public bool AllValid => Rows.Count > 0 && Rows.All(r => r.Request is not null);
}
