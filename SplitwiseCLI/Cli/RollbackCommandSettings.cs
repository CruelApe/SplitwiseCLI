using System.ComponentModel;
using Spectre.Console.Cli;

namespace SplitwiseCLI.Cli;

public sealed class RollbackCommandSettings : CommandSettings
{
    [CommandArgument(0, "<batchId>")]
    [Description("The batch id printed by 'import' at the end of a run (format yyyyMM-yyyyMM-xxxxxx).")]
    public required string BatchId { get; init; }

    [CommandOption("-y|--yes")]
    [Description("Skip the confirmation prompt and delete immediately.")]
    public bool Yes { get; init; }

    [CommandOption("--dry-run")]
    [Description("Preview matching expenses without deleting anything.")]
    public bool DryRun { get; init; }
}
