using SplitwiseCLI.Api;
using SplitwiseCLI.Configuration;
using SplitwiseCLI.Import;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Services;

public sealed class ImportOrchestrator(
    ISplitwiseClient client,
    CategoryLookupService categoryLookupService,
    GroupLookupService groupLookupService,
    AppConfig config,
    DuplicateExpenseDetector? duplicateDetector = null)
{
    // Kept optional (defaulting to a plain detector over the same client) so existing
    // callers that don't care about duplicate detection don't need to wire one up.
    private readonly DuplicateExpenseDetector _duplicateDetector = duplicateDetector ?? new DuplicateExpenseDetector(client);

    // Convenience wrapper for callers that don't need the pre-flight confirmation
    // step (e.g. tests) - builds the plan and creates every row immediately.
    public async Task<IReadOnlyList<ImportRowResult>> RunAsync(IReadOnlyList<string> filePaths, CancellationToken cancellationToken = default)
    {
        var plan = await PrepareAsync(filePaths, cancellationToken);
        return await CreateAsync(plan, cancellationToken: cancellationToken);
    }

    // Fully validates and maps every row - including the category/group existence
    // checks that used to only happen at creation time - without ever calling
    // CreateExpenseAsync, so the caller can decide whether to proceed before any
    // expense actually gets created.
    public async Task<ImportPlan> PrepareAsync(IReadOnlyList<string> filePaths, CancellationToken cancellationToken = default)
    {
        var defaultCurrency = await CurrencyResolver.ResolveAsync(client, config, cancellationToken);

        var categoryLookup = await categoryLookupService.LoadAsync(cancellationToken);
        var groupLookup = await groupLookupService.LoadAsync(cancellationToken);

        // Read + validate every file up front - duplicate detection needs the full set
        // of valid rows across all files before it can bound a single date-range query.
        var files = new List<(string FilePath, IReadOnlyList<(ExpenseRow Row, ValidatedExpenseRow? Validated, string? Error)> Validations, string? ReadError)>();

        foreach (var filePath in filePaths)
        {
            try
            {
                var rows = ExcelExpenseReader.Read(filePath);
                var validations = rows
                    .Select(row =>
                    {
                        var (validated, error) = ExpenseRowValidator.Validate(row);
                        return (Row: row, Validated: validated, Error: error);
                    })
                    .ToList();
                files.Add((filePath, validations, null));
            }
            catch (Exception ex)
            {
                files.Add((filePath, [], $"Failed to read file: {ex.Message}"));
            }
        }

        var allValidatedRows = files
            .SelectMany(f => f.Validations)
            .Where(v => v.Validated is not null)
            .Select(v => v.Validated!)
            .ToList();

        IReadOnlyList<Expense> duplicateCandidates = [];
        if (allValidatedRows.Count > 0)
        {
            duplicateCandidates = await _duplicateDetector.FindCandidatesAsync(
                allValidatedRows.Min(r => r.Date), allValidatedRows.Max(r => r.Date), cancellationToken: cancellationToken);
        }

        var planRows = new List<ImportPlanRow>();

        foreach (var file in files)
        {
            if (file.ReadError is not null)
            {
                planRows.Add(new ImportPlanRow(file.FilePath, 0, null, null, file.ReadError, null));
                continue;
            }

            // Computed once per file (before any row is created) so every row's
            // created/failed expense can be traced back to the same rollback tag.
            var validDates = file.Validations
                .Where(v => v.Validated is not null)
                .Select(v => v.Validated!.Date)
                .ToList();
            var batchId = validDates.Count > 0 ? BatchId.Generate(validDates) : null;

            foreach (var (row, validated, validationError) in file.Validations)
            {
                if (validated is null)
                {
                    planRows.Add(new ImportPlanRow(row.SourceFile, row.RowNumber, row.Description, null, validationError, batchId));
                    continue;
                }

                var duplicateReason = DuplicateExpenseDetector.FindDuplicateReason(validated, duplicateCandidates);
                var (request, mappingError) = ExpenseMapper.Map(
                    validated, categoryLookup.SubcategoryNamesById, groupLookup.GroupsById, defaultCurrency, batchId);
                planRows.Add(new ImportPlanRow(row.SourceFile, row.RowNumber, validated.Description, request, mappingError, batchId, duplicateReason));
            }
        }

        return new ImportPlan(planRows);
    }

    // Creates every row in the plan that mapped successfully; rows that failed
    // validation/mapping are carried straight through as failures. One row's API
    // failure must never abort the rest. Rows flagged as possible duplicates are
    // skipped (not treated as failures) unless includeDuplicates is set.
    // onRowProcessed is a plain callback (not a Spectre.Console type) so this
    // service stays UI-agnostic - the CLI layer wires it up to a progress bar.
    public async Task<IReadOnlyList<ImportRowResult>> CreateAsync(
        ImportPlan plan, bool includeDuplicates = false, Action<int>? onRowProcessed = null, CancellationToken cancellationToken = default)
    {
        var results = new List<ImportRowResult>();

        foreach (var row in plan.Rows)
        {
            if (row.Request is null)
            {
                results.Add(new ImportRowResult(row.SourceFile, row.RowNumber, row.Description, false, row.Error, null, row.BatchId));
            }
            else if (row.DuplicateReason is not null && !includeDuplicates)
            {
                results.Add(new ImportRowResult(
                    row.SourceFile, row.RowNumber, row.Description, true, null, null, row.BatchId,
                    Cost: row.Request.Cost, Date: row.Request.Date, CategoryId: row.Request.CategoryId,
                    GroupId: row.Request.GroupId, Details: row.Request.Details, DuplicateReason: row.DuplicateReason));
            }
            else
            {
                try
                {
                    var response = await client.CreateExpenseAsync(row.Request, cancellationToken);
                    var expenseId = response.Expenses?.FirstOrDefault()?.Id;
                    results.Add(new ImportRowResult(
                        row.SourceFile, row.RowNumber, row.Description, true, null, expenseId, row.BatchId,
                        Cost: row.Request.Cost, Date: row.Request.Date, CategoryId: row.Request.CategoryId,
                        GroupId: row.Request.GroupId, Details: row.Request.Details, DuplicateReason: row.DuplicateReason));
                }
                catch (Exception ex)
                {
                    results.Add(new ImportRowResult(row.SourceFile, row.RowNumber, row.Description, false, $"API error: {ex.Message}", null, row.BatchId));
                }
            }

            onRowProcessed?.Invoke(results.Count);
        }

        return results;
    }
}
