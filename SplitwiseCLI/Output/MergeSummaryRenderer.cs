using Spectre.Console;
using SplitwiseCLI.Services;

namespace SplitwiseCLI.Output;

public static class MergeSummaryRenderer
{
    public static void Render(MergePlan plan, string outputPath, int fileCount)
    {
        AnsiConsole.MarkupLine(
            $"[bold]{plan.Rows.Count}[/] row(s) merged from [bold]{fileCount}[/] file(s) into '[bold]{outputPath.EscapeMarkup()}[/]'.");

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
