using Spectre.Console;
using SplitwiseCLI.Services;

namespace SplitwiseCLI.Output;

public static class MergeSummaryRenderer
{
    public static void Render(MergePlan plan, string outputPath, int fileCount)
    {
        AnsiConsole.MarkupLine(
            $"[bold]{plan.AllRows.Count}[/] row(s) merged from [bold]{fileCount}[/] file(s) into '[bold]{outputPath.EscapeMarkup()}[/]'.");

        // Non-expense rows live on their own sheet, which `import` doesn't read - say
        // so rather than let them silently disappear from the Expenses sheet.
        if (plan.NonExpenseRows.Count > 0)
        {
            AnsiConsole.MarkupLine(
                $"[bold]{plan.NonExpenseRows.Count}[/] of them were classified as non-expense and written to the 'Non-Expense' sheet.");
        }

        var unassigned = plan.Rows.Count(r => r.CategoryId is null || r.GroupId is null);
        if (unassigned > 0)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]{unassigned}[/] expense row(s) matched no classification rule and have a blank Category/Group - fill them in on the 'Expenses' sheet before importing.");
        }

        if (plan.Issues.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine($"[red]{plan.Issues.Count} row(s) were skipped[/] due to validation errors:");

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("File");
        table.AddColumn("Row");
        table.AddColumn("Description");
        table.AddColumn("Reason");

        foreach (var issue in plan.Issues)
        {
            table.AddRow(
                Path.GetFileName(issue.SourceFile).EscapeMarkup(),
                issue.RowNumber == 0 ? "-" : issue.RowNumber.ToString(),
                (issue.Description ?? "-").EscapeMarkup(),
                issue.Error.EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }
}
