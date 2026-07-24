using System.ComponentModel;
using Spectre.Console.Cli;

namespace SplitwiseCLI.Cli;

public sealed class ImportCommandSettings : CommandSettings
{
    [CommandArgument(0, "<path>")]
    [Description("A file, directory, or glob pattern (e.g. \"C:/expenses/*.xlsx\") of Excel files to import.")]
    public required string Path { get; init; }

    [CommandOption("-y|--yes")]
    [Description("Skip the confirmation prompt shown when every row validates cleanly.")]
    public bool Yes { get; init; }
}
