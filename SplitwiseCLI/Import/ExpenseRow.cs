namespace SplitwiseCLI.Import;

public sealed class ExpenseRow
{
    public required string SourceFile { get; init; }
    public required int RowNumber { get; init; }
    public string? Description { get; init; }
    public string? Details { get; init; }
    public string? RawCost { get; init; }
    public string? RawDate { get; init; }
    public string? Category { get; init; }
    public string? Group { get; init; }

    // Set when the row itself couldn't be read (e.g. a malformed cell) so the
    // reader never has to throw and abort the rest of the file.
    public string? ParseError { get; init; }
}
