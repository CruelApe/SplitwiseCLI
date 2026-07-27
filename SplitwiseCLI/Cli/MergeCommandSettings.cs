using System.ComponentModel;
using Spectre.Console.Cli;

namespace SplitwiseCLI.Cli;

public sealed class MergeCommandSettings : CommandSettings
{
    [CommandArgument(0, "<paths>")]
    [Description("One or more files, directories, or glob patterns (e.g. \"C:/expenses/*.xlsx\") of already-formatted import Excel files and/or recognized PDF bank/credit-card statements to merge.")]
    public required string[] Paths { get; init; }

    [CommandOption("-o|--output <path>")]
    [Description("Output workbook path. Defaults to 'Expenses_<MonthRange>.xlsx' in the merged files' shared folder if they all come from the same one, otherwise in a 'Merged Files' folder (created if needed) in the current directory.")]
    public string? Output { get; init; }
}
