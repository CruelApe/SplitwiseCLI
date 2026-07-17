using Spectre.Console;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Output;

public static class CommentRenderer
{
    public static void RenderList(IReadOnlyList<Comment> comments)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Date");
        table.AddColumn("User");
        table.AddColumn("Comment");

        foreach (var comment in comments.OrderBy(c => c.CreatedAt))
        {
            table.AddRow(
                comment.CreatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-",
                comment.User is null ? "-" : $"{comment.User.FirstName} {comment.User.LastName}".Trim().EscapeMarkup(),
                (comment.Content ?? "-").EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }
}
