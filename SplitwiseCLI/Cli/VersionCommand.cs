using Spectre.Console;
using Spectre.Console.Cli;

namespace SplitwiseCLI.Cli;

public sealed class VersionCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"SplitwiseCLI [green]{AppInfo.Version.EscapeMarkup()}[/]");
        return 0;
    }
}
