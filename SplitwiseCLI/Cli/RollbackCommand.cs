using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Output;
using SplitwiseCLI.Services;

namespace SplitwiseCLI.Cli;

public sealed class RollbackCommand(SplitwiseClientFactory clientFactory) : AsyncCommand<RollbackCommandSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, RollbackCommandSettings settings, CancellationToken cancellationToken)
    {
        var (client, _) = clientFactory.Create();
        var orchestrator = new RollbackOrchestrator(client, new ExpenseSearchService(client));

        var matches = await AnsiConsole.Status()
            .StartAsync("Searching for tagged expenses...", ctx => orchestrator.FindMatchesAsync(
                settings.BatchId,
                onExpensesScanned: count => ctx.Status($"Searching for tagged expenses... ({count} scanned)"),
                cancellationToken));

        if (matches.Count == 0)
        {
            AnsiConsole.MarkupLine($"No expenses found tagged with batch id [bold]{settings.BatchId.EscapeMarkup()}[/].");
            return 0;
        }

        AnsiConsole.MarkupLine($"Found [bold]{matches.Count}[/] expense(s) tagged with batch id [bold]{settings.BatchId.EscapeMarkup()}[/]:");
        ExpenseRenderer.RenderList(matches);

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[grey]Dry run - no expenses were deleted.[/]");
            return 0;
        }

        if (!settings.Yes && !AnsiConsole.Confirm($"Delete these {matches.Count} expense(s)? This cannot be undone."))
        {
            AnsiConsole.MarkupLine("[yellow]Aborted - no expenses were deleted.[/]");
            return 1;
        }

        var results = (IReadOnlyList<RollbackRowResult>)[];
        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Deleting expenses", maxValue: matches.Count);
                results = await orchestrator.DeleteAsync(
                    matches, onExpenseProcessed: count => task.Value = count, cancellationToken: cancellationToken);
            });

        RollbackSummaryRenderer.RenderResult(results);

        return results.Any(r => !r.Success) ? 1 : 0;
    }
}
