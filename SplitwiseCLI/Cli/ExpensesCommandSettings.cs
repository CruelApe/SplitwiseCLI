using System.ComponentModel;
using Spectre.Console.Cli;

namespace SplitwiseCLI.Cli;

public sealed class ExpensesCommandSettings : CommandSettings
{
    [CommandOption("--group <ID>")]
    [Description("Only show expenses in this group.")]
    public long? GroupId { get; init; }

    [CommandOption("--friend <ID>")]
    [Description("Only show expenses between you and this friend.")]
    public long? FriendId { get; init; }

    [CommandOption("--dated-after <DATE>")]
    [Description("Only show expenses dated on or after this date (e.g. 2026-01-01).")]
    public DateTimeOffset? DatedAfter { get; init; }

    [CommandOption("--dated-before <DATE>")]
    [Description("Only show expenses dated on or before this date.")]
    public DateTimeOffset? DatedBefore { get; init; }

    [CommandOption("--updated-after <DATE>")]
    [Description("Only show expenses updated on or after this date.")]
    public DateTimeOffset? UpdatedAfter { get; init; }

    [CommandOption("--updated-before <DATE>")]
    [Description("Only show expenses updated on or before this date.")]
    public DateTimeOffset? UpdatedBefore { get; init; }

    [CommandOption("--limit <N>")]
    [Description("Maximum number of expenses to return (Splitwise default: 20).")]
    public int? Limit { get; init; }

    [CommandOption("--offset <N>")]
    [Description("Number of expenses to skip, for paging.")]
    public int? Offset { get; init; }
}
