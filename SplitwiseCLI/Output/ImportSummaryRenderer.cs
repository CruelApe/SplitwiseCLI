using System.Globalization;
using Spectre.Console;
using SplitwiseCLI.Services;

namespace SplitwiseCLI.Output;

public static class ImportSummaryRenderer
{
    public static void Render(IReadOnlyList<ImportRowResult> results)
    {
        var duplicateCount = results.Count(r => r.DuplicateReason is not null && r.ExpenseId is null);
        var successCount = results.Count(r => r.Success) - duplicateCount;
        var failureCount = results.Count - successCount - duplicateCount;

        AnsiConsole.MarkupLine(
            $"Processed [bold]{results.Count}[/] row(s): [green]{successCount} created[/], " +
            $"[{(duplicateCount > 0 ? "yellow" : "grey")}]{duplicateCount} skipped as duplicate(s)[/], " +
            $"[{(failureCount > 0 ? "red" : "grey")}]{failureCount} failed[/].");

        RenderSuccesses(results);
        RenderSkippedDuplicates(results);

        var failures = results.Where(r => !r.Success).ToList();
        if (failures.Count > 0)
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("File");
            table.AddColumn("Row");
            table.AddColumn("Description");
            table.AddColumn("Reason");

            foreach (var failure in failures)
            {
                table.AddRow(
                    Path.GetFileName(failure.SourceFile).EscapeMarkup(),
                    failure.RowNumber == 0 ? "-" : failure.RowNumber.ToString(),
                    (failure.Description ?? "-").EscapeMarkup(),
                    (failure.ErrorMessage ?? "Unknown error").EscapeMarkup());
            }

            AnsiConsole.Write(table);
        }

        RenderBatchIds(results);
    }

    private static void RenderSuccesses(IReadOnlyList<ImportRowResult> results)
    {
        var successes = results.Where(r => r.Success && r.ExpenseId is not null).ToList();
        if (successes.Count == 0)
        {
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Expense Id");
        table.AddColumn("File");
        table.AddColumn("Description");
        table.AddColumn("Cost");
        table.AddColumn("Date");
        table.AddColumn("Category Id");
        table.AddColumn("Group Id");
        table.AddColumn("Details");

        foreach (var success in successes)
        {
            table.AddRow(
                success.ExpenseId?.ToString() ?? "-",
                Path.GetFileName(success.SourceFile).EscapeMarkup(),
                (success.Description ?? "-").EscapeMarkup(),
                (success.Cost ?? "-").EscapeMarkup(),
                FormatDate(success.Date).EscapeMarkup(),
                success.CategoryId?.ToString() ?? "-",
                success.GroupId?.ToString() ?? "-",
                (success.Details ?? "-").EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }

    private static void RenderSkippedDuplicates(IReadOnlyList<ImportRowResult> results)
    {
        var duplicates = results.Where(r => r.DuplicateReason is not null && r.ExpenseId is null).ToList();
        if (duplicates.Count == 0)
        {
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("File");
        table.AddColumn("Row");
        table.AddColumn("Description");
        table.AddColumn("Reason");

        foreach (var duplicate in duplicates)
        {
            table.AddRow(
                Path.GetFileName(duplicate.SourceFile).EscapeMarkup(),
                duplicate.RowNumber == 0 ? "-" : duplicate.RowNumber.ToString(),
                (duplicate.Description ?? "-").EscapeMarkup(),
                duplicate.DuplicateReason!.EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }

    private static string FormatDate(string? isoDate)
    {
        if (isoDate is null)
        {
            return "-";
        }

        return DateTime.TryParse(isoDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
            ? date.ToString("yyyy-MM-dd")
            : isoDate;
    }

    private static void RenderBatchIds(IReadOnlyList<ImportRowResult> results)
    {
        var batchIds = results
            .Where(r => r.BatchId is not null)
            .Select(r => (File: Path.GetFileName(r.SourceFile), r.BatchId))
            .Distinct()
            .OrderBy(x => x.File, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (batchIds.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine(
            "[bold]Batch id(s)[/] (save these to undo this import later with 'splitwise rollback <batchId>'):");
        foreach (var (file, batchId) in batchIds)
        {
            AnsiConsole.MarkupLine($"  [grey]{file.EscapeMarkup()}[/]: [bold]{batchId!.EscapeMarkup()}[/]");
        }
    }
}
