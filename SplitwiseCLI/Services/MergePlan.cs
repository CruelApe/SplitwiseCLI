using SplitwiseCLI.Models;

namespace SplitwiseCLI.Services;

// CategoryId/GroupId are null for rows extracted from a PDF statement - those
// are left blank for the user to review and fill in by hand, unlike xlsx-sourced
// rows, which always carry the id already present in their source spreadsheet.
public sealed record MergedExpenseRow(
    string SourceFile,
    string Description,
    decimal Cost,
    DateTime Date,
    long? CategoryId,
    long? GroupId,
    string? Details);

public sealed record MergeRowIssue(string SourceFile, int RowNumber, string? Description, string Error);

// Categories/Groups are the full live lists from the account (not filtered down to
// only the ids referenced by Rows) - the output workbook's reference sheets are meant
// to be a full, browsable dump the user can check while editing the merged file.
public sealed record MergePlan(
    IReadOnlyList<MergedExpenseRow> Rows,
    IReadOnlyList<MergeRowIssue> Issues,
    IReadOnlyList<Category> Categories,
    IReadOnlyList<Group> Groups,
    string DefaultCurrency);
