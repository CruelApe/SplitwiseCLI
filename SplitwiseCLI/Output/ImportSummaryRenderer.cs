using System.Globalization;
using Spectre.Console;
using SplitwiseCLI.Services;

namespace SplitwiseCLI.Output;

public static class ImportSummaryRenderer
{
    public static void Render(IReadOnlyList<ImportRowResult> results)
    {
        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count - successCount;

        AnsiConsole.MarkupLine(
            $"Processed [bold]{results.Count}[/] row(s): [green]{successCount} succeeded[/], " +
            $"[{(failureCount > 0 ? "red" : "grey")}]{failureCount} failed[/].");

        RenderSuccesses(results);

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
        var successes = results.Where(r => r.Success).ToList();
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
