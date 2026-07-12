using Spectre.Console.Cli;
using SplitwiseCLI.Output;

namespace SplitwiseCLI.Cli;

public sealed class FriendsCommand(SplitwiseClientFactory clientFactory) : AsyncCommand
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var (client, _) = clientFactory.Create();
        var friends = await client.GetFriendsAsync(cancellationToken);
        FriendRenderer.RenderList(friends);
        return 0;
    }
}
