using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Output;

namespace SplitwiseCLI.Cli;

public sealed class ExpenseCommand(SplitwiseClientFactory clientFactory) : AsyncCommand<IdCommandSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, IdCommandSettings settings, CancellationToken cancellationToken)
    {
        var (client, _) = clientFactory.Create();
        var expense = await AnsiConsole.Status().StartAsync("Loading expense...", _ => client.GetExpenseAsync(settings.Id, cancellationToken));
        ExpenseRenderer.RenderDetail(expense);
        return 0;
    }
}
