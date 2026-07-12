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

        var failures = results.Where(r => !r.Success).ToList();
        if (failures.Count == 0)
        {
            return;
        }

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
}
