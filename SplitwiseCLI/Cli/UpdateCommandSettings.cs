using System.ComponentModel;
using Spectre.Console.Cli;

namespace SplitwiseCLI.Cli;

public sealed class UpdateCommandSettings : CommandSettings
{
    [CommandOption("--check")]
    [Description("Only check whether a newer version is available; never download or apply anything.")]
    public bool CheckOnly { get; init; }

    [CommandOption("-y|--yes")]
    [Description("Skip the confirmation prompt and apply the update immediately.")]
    public bool Yes { get; init; }
}
