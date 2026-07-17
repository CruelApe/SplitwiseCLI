using Spectre.Console.Cli;
using SplitwiseCLI.Output;

namespace SplitwiseCLI.Cli;

public sealed class GroupCommand(SplitwiseClientFactory clientFactory) : AsyncCommand<IdCommandSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, IdCommandSettings settings, CancellationToken cancellationToken)
    {
        var (client, _) = clientFactory.Create();
        var group = await client.GetGroupAsync(settings.Id, cancellationToken);
        GroupRenderer.RenderDetail(group);
        return 0;
    }
}
