using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Output;

namespace SplitwiseCLI.Cli;

public sealed class GroupsCommand(SplitwiseClientFactory clientFactory) : AsyncCommand
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var (client, _) = clientFactory.Create();
        var groups = await AnsiConsole.Status().StartAsync("Loading groups...", _ => client.GetGroupsAsync(cancellationToken));
        GroupRenderer.RenderList(groups);
        return 0;
    }
}
