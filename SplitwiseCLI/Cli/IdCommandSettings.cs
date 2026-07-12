using System.ComponentModel;
using Spectre.Console.Cli;

namespace SplitwiseCLI.Cli;

// Shared by every command that takes a single Splitwise numeric id (user, group,
// friend, expense, or - for "comments" - the owning expense's id).
public sealed class IdCommandSettings : CommandSettings
{
    [CommandArgument(0, "<id>")]
    [Description("The Splitwise numeric id.")]
    public required long Id { get; init; }
}
