using Spectre.Console;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Output;

public static class GroupRenderer
{
    public static void RenderList(IReadOnlyList<Group> groups)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Id");
        table.AddColumn("Name");
        table.AddColumn("Members");

        foreach (var group in groups.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(group.Id.ToString(), (group.Name ?? "-").EscapeMarkup(), group.Members.Count.ToString());
        }

        AnsiConsole.Write(table);
    }

    public static void RenderDetail(Group group)
    {
        AnsiConsole.MarkupLine($"[bold]{(group.Name ?? "-").EscapeMarkup()}[/] (id: {group.Id})");

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Id");
        table.AddColumn("Name");
        table.AddColumn("Email");

        foreach (var member in group.Members)
        {
            table.AddRow(
                member.Id.ToString(),
                $"{member.FirstName} {member.LastName}".Trim().EscapeMarkup(),
                (member.Email ?? "-").EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }
}
