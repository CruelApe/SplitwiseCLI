using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Output;

namespace SplitwiseCLI.Cli;

public sealed class NotificationsCommand(SplitwiseClientFactory clientFactory) : AsyncCommand
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var (client, _) = clientFactory.Create();
        var notifications = await AnsiConsole.Status().StartAsync("Loading notifications...", _ => client.GetNotificationsAsync(cancellationToken));
        NotificationRenderer.RenderList(notifications);
        return 0;
    }
}
