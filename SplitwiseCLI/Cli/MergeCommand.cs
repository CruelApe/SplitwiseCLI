using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Import;
using SplitwiseCLI.Output;
using SplitwiseCLI.Services;
using SplitwiseCLI.Statements;

namespace SplitwiseCLI.Cli;

public sealed class MergeCommand(SplitwiseClientFactory clientFactory) : AsyncCommand<MergeCommandSettings>
{
    private static readonly string[] AcceptedExtensions = ["*.xlsx", "*.pdf"];

    protected override async Task<int> ExecuteAsync(CommandContext context, MergeCommandSettings settings, CancellationToken cancellationToken)
    {
        var files = settings.Paths
            .SelectMany(p => FileResolver.Resolve(p, AcceptedExtensions))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]No files matched pattern(s) '{string.Join("', '", settings.Paths).EscapeMarkup()}'.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"Found [bold]{files.Count}[/] file(s) to merge.");

        var (client, config) = clientFactory.Create();
        var orchestrator = new MergeOrchestrator(
            client, new CategoryLookupService(client), new GroupLookupService(client), config, new PdfPigTextExtractor());

        var plan = await AnsiConsole.Status()
            .StartAsync("Reading and merging rows...", _ => orchestrator.PrepareAsync(files, cancellationToken));

        string outputPath;
        if (settings.Output is not null)
        {
            outputPath = Path.GetFullPath(settings.Output);
        }
        else
        {
            var outputDirectory = MergeOrchestrator.DetermineDefaultOutputDirectory(files);
            Directory.CreateDirectory(outputDirectory);
            outputPath = Path.GetFullPath(Path.Combine(outputDirectory, MergeOrchestrator.BuildDefaultOutputFileName(plan.Rows)));
        }

        orchestrator.Write(plan, outputPath);

        MergeSummaryRenderer.Render(plan, outputPath, files.Count);

        return plan.Issues.Count > 0 ? 1 : 0;
    }
}
