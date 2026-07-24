using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Output;
using SplitwiseCLI.Services;

namespace SplitwiseCLI.Cli;

public sealed class CategoriesCommand(SplitwiseClientFactory clientFactory) : AsyncCommand
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var (client, _) = clientFactory.Create();
        var categoryLookupService = new CategoryLookupService(client);

        var lookup = await AnsiConsole.Status().StartAsync("Loading categories...", _ => categoryLookupService.LoadAsync(cancellationToken));
        CategoryTreeRenderer.Render(lookup.Categories);

        return 0;
    }
}
