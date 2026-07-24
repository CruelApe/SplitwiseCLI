using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Models;
using SplitwiseCLI.Output;

namespace SplitwiseCLI.Cli;

public sealed class ExpensesCommand(SplitwiseClientFactory clientFactory) : AsyncCommand<ExpensesCommandSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ExpensesCommandSettings settings, CancellationToken cancellationToken)
    {
        var (client, _) = clientFactory.Create();
        var filter = new ExpenseFilter(
            settings.GroupId,
            settings.FriendId,
            settings.DatedAfter,
            settings.DatedBefore,
            settings.UpdatedAfter,
            settings.UpdatedBefore,
            settings.Limit,
            settings.Offset);

        var expenses = await AnsiConsole.Status().StartAsync("Loading expenses...", _ => client.GetExpensesAsync(filter, cancellationToken));
        ExpenseRenderer.RenderList(expenses);
        return 0;
    }
}
