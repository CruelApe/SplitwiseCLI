using Spectre.Console;
using SplitwiseCLI.Services;

namespace SplitwiseCLI.Output;

public static class RollbackSummaryRenderer
{
    public static void RenderResult(IReadOnlyList<RollbackRowResult> results)
    {
        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count - successCount;

        AnsiConsole.MarkupLine(
            $"Deleted [bold]{results.Count}[/] expense(s): [green]{successCount} succeeded[/], " +
            $"[{(failureCount > 0 ? "red" : "grey")}]{failureCount} failed[/].");

        var failures = results.Where(r => !r.Success).ToList();
        if (failures.Count == 0)
        {
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Expense Id");
        table.AddColumn("Description");
        table.AddColumn("Reason");

        foreach (var failure in failures)
        {
            table.AddRow(
                failure.ExpenseId.ToString(),
                (failure.Description ?? "-").EscapeMarkup(),
                (failure.ErrorMessage ?? "Unknown error").EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }
}
