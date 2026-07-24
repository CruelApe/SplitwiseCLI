using ClosedXML.Excel;

namespace SplitwiseCLI.Import;

public static class ExcelExpenseReader
{
    private static readonly string[] ExpectedHeaders = ["Description", "Cost", "Date", "Category", "Group"];

    public static IReadOnlyList<ExpenseRow> Read(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.First();

        var headerRow = worksheet.FirstRowUsed()
            ?? throw new InvalidOperationException($"'{filePath}' has no header row.");
        var columnIndex = BuildColumnIndex(headerRow, filePath);

        return worksheet.RowsUsed()
            .Skip(1)
            .Select(row => ReadRow(row, filePath, columnIndex))
            .ToList();
    }

    private static Dictionary<string, int> BuildColumnIndex(IXLRow headerRow, string filePath)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(header))
            {
                index[header] = cell.Address.ColumnNumber;
            }
        }

        var missing = ExpectedHeaders.Where(h => !index.ContainsKey(h)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"'{filePath}': missing expected column(s) [{string.Join(", ", missing)}]. " +
                $"Expected header row: {string.Join(", ", ExpectedHeaders)}.");
        }

        return index;
    }

    private static ExpenseRow ReadRow(IXLRow row, string filePath, Dictionary<string, int> columnIndex)
    {
        try
        {
            return new ExpenseRow
            {
                SourceFile = filePath,
                RowNumber = row.RowNumber(),
                Description = GetCellText(row, columnIndex, "Description"),
                Details = GetCellText(row, columnIndex, "Details"),
                RawCost = GetCellText(row, columnIndex, "Cost"),
                RawDate = GetCellText(row, columnIndex, "Date"),
                RawCategory = GetCellText(row, columnIndex, "Category"),
                RawGroup = GetCellText(row, columnIndex, "Group"),
            };
        }
        catch (Exception ex)
        {
            return new ExpenseRow
            {
                SourceFile = filePath,
                RowNumber = row.RowNumber(),
                ParseError = $"Failed to read row {row.RowNumber()}: {ex.Message}",
            };
        }
    }

    private static string? GetCellText(IXLRow row, Dictionary<string, int> columnIndex, string columnName)
    {
        // Optional columns (e.g. "Details") may not exist in the sheet at all.
        if (!columnIndex.TryGetValue(columnName, out var columnNumber))
        {
            return null;
        }

        var cell = row.Cell(columnNumber);
        if (cell.IsEmpty())
        {
            return null;
        }

        // Dates stored as Excel serial numbers need explicit formatting, otherwise
        // GetString() would return the raw serial number instead of a date string.
        if (cell.DataType == XLDataType.DateTime)
        {
            return cell.GetDateTime().ToString("O");
        }

        return cell.GetString().Trim();
    }
}
