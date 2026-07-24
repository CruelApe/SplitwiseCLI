using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Output;

namespace SplitwiseCLI.Cli;

public sealed class MeCommand(SplitwiseClientFactory clientFactory) : AsyncCommand
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var (client, _) = clientFactory.Create();
        var user = await AnsiConsole.Status().StartAsync("Loading your account...", _ => client.GetCurrentUserAsync(cancellationToken));
        UserRenderer.Render(user);
        return 0;
    }
}
