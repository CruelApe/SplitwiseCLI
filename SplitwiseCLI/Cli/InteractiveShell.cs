using Spectre.Console;
using Spectre.Console.Cli;

namespace SplitwiseCLI.Cli;

// Entered when the app is launched with no arguments (e.g. hitting F5 in Visual
// Studio) so the console window stays open and commands can be typed one at a
// time, instead of the process exiting immediately after printing usage.
public static class InteractiveShell
{
    private static readonly string[] ExitCommands = ["exit", "quit"];

    public static async Task RunAsync(CommandApp app)
    {
        AnsiConsole.MarkupLine(
            "[bold]SplitwiseCLI[/] interactive mode. Type a command (e.g. [grey]categories[/], " +
            "[grey]import <path>[/], [grey]--help[/]) or [grey]exit[/] to quit.");

        while (true)
        {
            AnsiConsole.Markup("[bold]splitwise>[/] ");
            var line = Console.ReadLine();

            if (line is null || ExitCommands.Contains(line.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            await app.RunAsync(CommandLineTokenizer.Tokenize(line));
        }
    }
}
