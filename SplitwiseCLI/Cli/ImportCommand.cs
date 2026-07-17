using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Import;
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

        var results = await orchestrator.RunAsync(files, cancellationToken);
        ImportSummaryRenderer.Render(results);

        return results.Any(r => !r.Success) ? 1 : 0;
    }
}
