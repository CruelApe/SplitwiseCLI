using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Output;

namespace SplitwiseCLI.Cli;

public sealed class CommentsCommand(SplitwiseClientFactory clientFactory) : AsyncCommand<IdCommandSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, IdCommandSettings settings, CancellationToken cancellationToken)
    {
        var (client, _) = clientFactory.Create();
        var comments = await AnsiConsole.Status().StartAsync("Loading comments...", _ => client.GetCommentsAsync(settings.Id, cancellationToken));
        CommentRenderer.RenderList(comments);
        return 0;
    }
}
