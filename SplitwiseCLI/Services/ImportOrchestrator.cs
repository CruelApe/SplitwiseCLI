using SplitwiseCLI.Api;
using SplitwiseCLI.Configuration;
using SplitwiseCLI.Import;

namespace SplitwiseCLI.Services;

public sealed class ImportOrchestrator(
    ISplitwiseClient client,
    CategoryLookupService categoryLookupService,
    GroupLookupService groupLookupService,
    AppConfig config)
{
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
        var currentUser = await client.GetCurrentUserAsync(cancellationToken);
        var defaultCurrency = config.DefaultCurrencyOverride ?? currentUser.DefaultCurrency
            ?? throw new SplitwiseApiException(
                "Could not determine a currency to use: no SPLITWISE_DEFAULT_CURRENCY override is set and the " +
                "Splitwise account has no default currency.");

        var categoryLookup = await categoryLookupService.LoadAsync(cancellationToken);
        var groupLookup = await groupLookupService.LoadAsync(cancellationToken);

        var planRows = new List<ImportPlanRow>();

        foreach (var filePath in filePaths)
        {
            IReadOnlyList<ExpenseRow> rows;
            try
            {
                rows = ExcelExpenseReader.Read(filePath);
            }
            catch (Exception ex)
            {
                planRows.Add(new ImportPlanRow(filePath, 0, null, null, $"Failed to read file: {ex.Message}", null));
                continue;
            }

            var validations = rows
                .Select(row => (Row: row, Validation: ExpenseRowValidator.Validate(row)))
                .ToList();

            // Computed once per file (before any row is created) so every row's
            // created/failed expense can be traced back to the same rollback tag.
            var validDates = validations
                .Where(v => v.Validation.Row is not null)
                .Select(v => v.Validation.Row!.Date)
                .ToList();
            var batchId = validDates.Count > 0 ? BatchId.Generate(validDates) : null;

            foreach (var (row, validation) in validations)
            {
                var (validated, validationError) = validation;
                if (validated is null)
                {
                    planRows.Add(new ImportPlanRow(row.SourceFile, row.RowNumber, row.Description, null, validationError, batchId));
                    continue;
                }

                var (request, mappingError) = ExpenseMapper.Map(
                    validated, categoryLookup.SubcategoryNamesById, groupLookup.GroupsById, defaultCurrency, batchId);
                planRows.Add(new ImportPlanRow(row.SourceFile, row.RowNumber, validated.Description, request, mappingError, batchId));
            }
        }

        return new ImportPlan(planRows);
    }

    // Creates every row in the plan that mapped successfully; rows that failed
    // validation/mapping are carried straight through as failures. One row's API
    // failure must never abort the rest.
    // onRowProcessed is a plain callback (not a Spectre.Console type) so this
    // service stays UI-agnostic - the CLI layer wires it up to a progress bar.
    public async Task<IReadOnlyList<ImportRowResult>> CreateAsync(
        ImportPlan plan, Action<int>? onRowProcessed = null, CancellationToken cancellationToken = default)
    {
        var results = new List<ImportRowResult>();

        foreach (var row in plan.Rows)
        {
            if (row.Request is null)
            {
                results.Add(new ImportRowResult(row.SourceFile, row.RowNumber, row.Description, false, row.Error, null, row.BatchId));
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
                        GroupId: row.Request.GroupId, Details: row.Request.Details));
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
