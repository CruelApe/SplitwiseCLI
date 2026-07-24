using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Output;

namespace SplitwiseCLI.Cli;

public sealed class FriendCommand(SplitwiseClientFactory clientFactory) : AsyncCommand<IdCommandSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, IdCommandSettings settings, CancellationToken cancellationToken)
    {
        var (client, _) = clientFactory.Create();
        var friend = await AnsiConsole.Status().StartAsync("Loading friend...", _ => client.GetFriendAsync(settings.Id, cancellationToken));
        FriendRenderer.RenderDetail(friend);
        return 0;
    }
}
