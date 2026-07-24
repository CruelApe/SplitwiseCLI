using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Output;

namespace SplitwiseCLI.Cli;

public sealed class CurrenciesCommand(SplitwiseClientFactory clientFactory) : AsyncCommand
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var (client, _) = clientFactory.Create();
        var currencies = await AnsiConsole.Status().StartAsync("Loading currencies...", _ => client.GetCurrenciesAsync(cancellationToken));
        CurrencyRenderer.RenderList(currencies);
        return 0;
    }
}
