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
    public async Task<IReadOnlyList<ImportRowResult>> RunAsync(IReadOnlyList<string> filePaths, CancellationToken cancellationToken = default)
    {
        var results = new List<ImportRowResult>();

        var currentUser = await client.GetCurrentUserAsync(cancellationToken);
        var defaultCurrency = config.DefaultCurrencyOverride ?? currentUser.DefaultCurrency
            ?? throw new SplitwiseApiException(
                "Could not determine a currency to use: no SPLITWISE_DEFAULT_CURRENCY override is set and the " +
                "Splitwise account has no default currency.");

        var categoryLookup = await categoryLookupService.LoadAsync(cancellationToken);
        var groupLookup = await groupLookupService.LoadAsync(cancellationToken);

        foreach (var filePath in filePaths)
        {
            IReadOnlyList<ExpenseRow> rows;
            try
            {
                rows = ExcelExpenseReader.Read(filePath);
            }
            catch (Exception ex)
            {
                results.Add(new ImportRowResult(filePath, 0, null, false, $"Failed to read file: {ex.Message}", null));
                continue;
            }

            foreach (var row in rows)
            {
                results.Add(await ProcessRowAsync(row, categoryLookup, groupLookup, defaultCurrency, cancellationToken));
            }
        }

        return results;
    }

    private async Task<ImportRowResult> ProcessRowAsync(
        ExpenseRow row,
        CategoryLookup categoryLookup,
        GroupLookup groupLookup,
        string defaultCurrency,
        CancellationToken cancellationToken)
    {
        var (validated, validationError) = ExpenseRowValidator.Validate(row);
        if (validated is null)
        {
            return Failure(row, validationError!);
        }

        var (request, mappingError) = ExpenseMapper.Map(
            validated, categoryLookup.SubcategoryIdsByName, groupLookup.GroupsByName, defaultCurrency);
        if (request is null)
        {
            return Failure(row, mappingError!);
        }

        try
        {
            var response = await client.CreateExpenseAsync(request, cancellationToken);
            var expenseId = response.Expenses?.FirstOrDefault()?.Id;
            return new ImportRowResult(row.SourceFile, row.RowNumber, validated.Description, true, null, expenseId);
        }
        catch (Exception ex)
        {
            // A per-row API failure must never abort the rest of the import.
            return Failure(row, $"API error: {ex.Message}");
        }
    }

    private static ImportRowResult Failure(ExpenseRow row, string error) =>
        new(row.SourceFile, row.RowNumber, row.Description, false, error, null);
}
