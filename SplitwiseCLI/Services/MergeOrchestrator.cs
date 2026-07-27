using System.Globalization;
using ClosedXML.Excel;
using SplitwiseCLI.Api;
using SplitwiseCLI.Configuration;
using SplitwiseCLI.Import;
using SplitwiseCLI.Models;
using SplitwiseCLI.Statements;

namespace SplitwiseCLI.Services;

public sealed class MergeOrchestrator(
    ISplitwiseClient client,
    CategoryLookupService categoryLookupService,
    GroupLookupService groupLookupService,
    AppConfig config,
    IPdfTextExtractor pdfTextExtractor)
{
    private readonly StatementParserRegistry _statementParserRegistry = new();

    // Reads and validates every row across every file (no expenses are created and
    // no per-row category/group existence check is done - merge just reshapes rows
    // it can parse, it doesn't need an id to already exist on the account).
    public async Task<MergePlan> PrepareAsync(IReadOnlyList<string> filePaths, CancellationToken cancellationToken = default)
    {
        var defaultCurrency = await CurrencyResolver.ResolveAsync(client, config, cancellationToken);
        var categoryLookup = await categoryLookupService.LoadAsync(cancellationToken);
        var groupLookup = await groupLookupService.LoadAsync(cancellationToken);

        var rows = new List<MergedExpenseRow>();
        var issues = new List<MergeRowIssue>();

        foreach (var filePath in filePaths)
        {
            switch (Path.GetExtension(filePath).ToLowerInvariant())
            {
                case ".xlsx":
                    ProcessXlsxFile(filePath, rows, issues);
                    break;
                case ".pdf":
                    ProcessPdfFile(filePath, rows, issues);
                    break;
                default:
                    issues.Add(new MergeRowIssue(filePath, 0, null,
                        $"Unsupported file type '{Path.GetExtension(filePath)}' - merge accepts .xlsx or .pdf files."));
                    break;
            }
        }

        // Group id 0 is Splitwise's pseudo-group for non-group expenses - excluded
        // here the same way GroupLookupService.GroupsById already excludes it.
        var groups = groupLookup.Groups.Where(g => g.Id != 0).ToList();
        return new MergePlan(rows, issues, categoryLookup.Categories, groups, defaultCurrency);
    }

    private static void ProcessXlsxFile(string filePath, List<MergedExpenseRow> rows, List<MergeRowIssue> issues)
    {
        IReadOnlyList<ExpenseRow> fileRows;
        try
        {
            fileRows = ExcelExpenseReader.Read(filePath);
        }
        catch (Exception ex)
        {
            issues.Add(new MergeRowIssue(filePath, 0, null, $"Failed to read file: {ex.Message}"));
            return;
        }

        foreach (var row in fileRows)
        {
            var (validated, error) = ExpenseRowValidator.Validate(row);
            if (validated is null)
            {
                issues.Add(new MergeRowIssue(row.SourceFile, row.RowNumber, row.Description, error!));
                continue;
            }

            rows.Add(new MergedExpenseRow(
                row.SourceFile, validated.Description, validated.Cost, validated.Date,
                validated.CategoryId, validated.GroupId, validated.Details));
        }
    }

    private void ProcessPdfFile(string filePath, List<MergedExpenseRow> rows, List<MergeRowIssue> issues)
    {
        string text;
        try
        {
            text = pdfTextExtractor.ExtractText(filePath);
        }
        catch (Exception ex)
        {
            issues.Add(new MergeRowIssue(filePath, 0, null, $"Failed to read PDF: {ex.Message}"));
            return;
        }

        try
        {
            rows.AddRange(_statementParserRegistry.Parse(filePath, text));
        }
        catch (Exception ex)
        {
            issues.Add(new MergeRowIssue(filePath, 0, null, ex.Message));
        }
    }

    // Falls back to the current month if there are no valid rows to derive a range
    // from - Write() still produces a workbook (with just the reference sheets) in
    // that case, and the caller needs some file name to save it under.
    public static string BuildDefaultOutputFileName(IReadOnlyList<MergedExpenseRow> rows)
    {
        if (rows.Count == 0)
        {
            return $"Expenses_{DateTime.Now:MMMM}.xlsx";
        }

        var min = rows.Min(r => r.Date);
        var max = rows.Max(r => r.Date);
        var minMonth = min.ToString("MMMM", CultureInfo.InvariantCulture);

        return min.Year == max.Year && min.Month == max.Month
            ? $"Expenses_{minMonth}.xlsx"
            : $"Expenses_{minMonth}-{max.ToString("MMMM", CultureInfo.InvariantCulture)}.xlsx";
    }

    // If every merged file lives in the same folder, the output belongs alongside
    // them - otherwise there's no single obvious folder to pick, so it goes in a
    // dedicated "Merged Files" folder instead of ending up wherever the command
    // happened to be run from.
    public static string DetermineDefaultOutputDirectory(IReadOnlyList<string> filePaths)
    {
        var directories = filePaths
            .Select(f => Path.GetDirectoryName(Path.GetFullPath(f)) ?? Directory.GetCurrentDirectory())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return directories.Count == 1
            ? directories[0]
            : Path.Combine(Directory.GetCurrentDirectory(), "Merged Files");
    }

    // Pure local file I/O - no Splitwise API calls happen here.
    public void Write(MergePlan plan, string outputPath)
    {
        using var workbook = new XLWorkbook();

        var categoryRows = plan.Categories
            .SelectMany(c => c.Subcategories.Select(s => (CategoryType: c.Name, s.Name, s.Id)))
            .ToList();

        WriteExpensesSheet(workbook, plan, categoryRows.Count);
        WriteCategoryReferenceSheet(workbook, categoryRows);
        WriteGroupReferenceSheet(workbook, plan.Groups);

        workbook.SaveAs(outputPath);
    }

    private static void WriteExpensesSheet(XLWorkbook workbook, MergePlan plan, int categoryRowCount)
    {
        var sheet = workbook.Worksheets.Add("Expenses");
        string[] headers =
        [
            "Cost", "Description", "Date", "Currency Code", "Category Name",
            "Group Name", "Split Equally", "Category", "Group", "Details",
        ];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var r = 2;
        foreach (var row in plan.Rows)
        {
            sheet.Cell(r, 1).Value = row.Cost;
            sheet.Cell(r, 2).Value = row.Description;
            sheet.Cell(r, 3).Value = row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            sheet.Cell(r, 4).Value = plan.DefaultCurrency;
            sheet.Cell(r, 5).FormulaA1 = $"=_xlfn.XLOOKUP(H{r},'Category Reference Data'!C:C,'Category Reference Data'!B:B,\"\")";
            sheet.Cell(r, 6).FormulaA1 = $"=_xlfn.XLOOKUP(I{r},'Group Reference Data'!A:A,'Group Reference Data'!B:B,\"\")";
            sheet.Cell(r, 7).Value = "true";
            // Left blank (rather than writing an empty string/0) when null, so a
            // PDF-derived row with no category/group yet reads as genuinely empty
            // in Excel and doesn't accidentally XLOOKUP-match a reference row.
            if (row.CategoryId is { } categoryId)
            {
                sheet.Cell(r, 8).Value = categoryId;
            }

            if (row.GroupId is { } groupId)
            {
                sheet.Cell(r, 9).Value = groupId;
            }

            sheet.Cell(r, 10).Value = row.Details ?? "";
            r++;
        }

        // Dropdowns targeting each reference sheet's id column, so picking a
        // Category/Group in Excel is a pick-from-list rather than a copy-pasted
        // number that has to be double-checked against the reference sheet by eye.
        var lastRow = plan.Rows.Count + 1;
        if (plan.Rows.Count > 0 && categoryRowCount > 0)
        {
            sheet.Range(2, 8, lastRow, 8).CreateDataValidation()
                .List($"='Category Reference Data'!$C$2:$C${categoryRowCount + 1}");
        }

        if (plan.Rows.Count > 0 && plan.Groups.Count > 0)
        {
            sheet.Range(2, 9, lastRow, 9).CreateDataValidation()
                .List($"='Group Reference Data'!$A$2:$A${plan.Groups.Count + 1}");
        }
    }

    private static void WriteCategoryReferenceSheet(
        XLWorkbook workbook, IReadOnlyList<(string CategoryType, string Name, long Id)> categoryRows)
    {
        var sheet = workbook.Worksheets.Add("Category Reference Data");
        sheet.Cell(1, 1).Value = "Category Type";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "Category Id";

        var r = 2;
        foreach (var (categoryType, name, id) in categoryRows)
        {
            sheet.Cell(r, 1).Value = categoryType;
            sheet.Cell(r, 2).Value = name;
            sheet.Cell(r, 3).Value = id;
            r++;
        }
    }

    private static void WriteGroupReferenceSheet(XLWorkbook workbook, IReadOnlyList<Group> groups)
    {
        var sheet = workbook.Worksheets.Add("Group Reference Data");
        sheet.Cell(1, 1).Value = "Group Id";
        sheet.Cell(1, 2).Value = "Group Name Reference";
        sheet.Cell(1, 3).Value = "Members Count";

        var r = 2;
        foreach (var group in groups)
        {
            sheet.Cell(r, 1).Value = group.Id;
            sheet.Cell(r, 2).Value = group.Name ?? "";
            sheet.Cell(r, 3).Value = group.Members.Count;
            r++;
        }
    }
}
