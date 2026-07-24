using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Api;
using SplitwiseCLI.Import;
using SplitwiseCLI.Models;
using SplitwiseCLI.Output;
using SplitwiseCLI.Services;

namespace SplitwiseCLI.Cli;

public sealed class ImportCommand(SplitwiseClientFactory clientFactory) : AsyncCommand<ImportCommandSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ImportCommandSettings settings, CancellationToken cancellationToken)
    {
        var files = FileResolver.Resolve(settings.Path);
        if (files.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]No files matched pattern '{settings.Path.EscapeMarkup()}'.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"Found [bold]{files.Count}[/] file(s) to import.");

        var (client, config) = clientFactory.Create();
        var orchestrator = new ImportOrchestrator(
            client, new CategoryLookupService(client), new GroupLookupService(client), config);

        var plan = await AnsiConsole.Status()
            .StartAsync("Validating rows...", _ => orchestrator.PrepareAsync(files, cancellationToken));

        // Only prompt when the whole run validated cleanly - if anything failed,
        // fall straight through to today's behavior (create what's valid, report the rest).
        if (plan.AllValid && !settings.Yes &&
            !AnsiConsole.Confirm($"{plan.Rows.Count} expense(s) across {files.Count} file(s) validated with no errors. Create them in Splitwise?"))
        {
            AnsiConsole.MarkupLine("[yellow]Aborted - no expenses were created.[/]");
            return 1;
        }

        var results = await CreateWithProgressAsync(orchestrator, plan, cancellationToken);
        ImportSummaryRenderer.Render(results);

        await OfferRollbackForPartiallyFailedBatchesAsync(client, results, settings.Yes, cancellationToken);

        return results.Any(r => !r.Success) ? 1 : 0;
    }

    // No rows means nothing to create - AddTask with a zero maxValue isn't a
    // meaningful progress bar, so skip it entirely in that case.
    private static async Task<IReadOnlyList<ImportRowResult>> CreateWithProgressAsync(
        ImportOrchestrator orchestrator, ImportPlan plan, CancellationToken cancellationToken)
    {
        if (plan.Rows.Count == 0)
        {
            return await orchestrator.CreateAsync(plan, cancellationToken: cancellationToken);
        }

        var results = (IReadOnlyList<ImportRowResult>)[];
        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Creating expenses", maxValue: plan.Rows.Count);
                results = await orchestrator.CreateAsync(
                    plan, onRowProcessed: count => task.Value = count, cancellationToken: cancellationToken);
            });

        return results;
    }

    // A batch that's part-succeeded, part-failed is left in a half-imported state -
    // offer to undo just the successful rows for that batch right away, rather than
    // making the user notice and run 'rollback' separately afterward.
    private static async Task OfferRollbackForPartiallyFailedBatchesAsync(
        ISplitwiseClient client, IReadOnlyList<ImportRowResult> results, bool skipPrompts, CancellationToken cancellationToken)
    {
        var partiallyFailedBatches = results
            .Where(r => r.BatchId is not null)
            .GroupBy(r => r.BatchId!)
            .Where(g => g.Any(r => r.Success) && g.Any(r => !r.Success))
            .ToList();

        if (partiallyFailedBatches.Count == 0)
        {
            return;
        }

        var rollbackOrchestrator = new RollbackOrchestrator(client, new ExpenseSearchService(client));

        foreach (var batch in partiallyFailedBatches)
        {
            var succeeded = batch.Where(r => r.Success && r.ExpenseId is not null).ToList();
            var failedCount = batch.Count(r => !r.Success);
            var file = Path.GetFileName(batch.First().SourceFile);

            if (skipPrompts)
            {
                AnsiConsole.MarkupLine(
                    $"[grey]{file.EscapeMarkup()}: {succeeded.Count} succeeded, {failedCount} failed for batch {batch.Key.EscapeMarkup()} - " +
                    $"skipping the rollback prompt due to --yes. Run 'splitwise rollback {batch.Key}' to undo if needed.[/]");
                continue;
            }

            var shouldRollBack = AnsiConsole.Confirm(
                $"{file.EscapeMarkup()}: {succeeded.Count} succeeded, {failedCount} failed for batch [bold]{batch.Key.EscapeMarkup()}[/]. " +
                "Roll back the succeeded expense(s) for this batch?",
                defaultValue: false);

            if (!shouldRollBack)
            {
                continue;
            }

            var toDelete = succeeded
                .Select(r => new Expense { Id = r.ExpenseId!.Value, Description = r.Description })
                .ToList();

            var rollbackResults = (IReadOnlyList<RollbackRowResult>)[];
            await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("Rolling back batch", maxValue: toDelete.Count);
                    rollbackResults = await rollbackOrchestrator.DeleteAsync(
                        toDelete, onExpenseProcessed: count => task.Value = count, cancellationToken: cancellationToken);
                });

            RollbackSummaryRenderer.RenderResult(rollbackResults);
        }
    }
}
