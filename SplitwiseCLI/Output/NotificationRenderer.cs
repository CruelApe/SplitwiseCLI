using Spectre.Console;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Output;

public static class NotificationRenderer
{
    // Per the Splitwise API docs' notification type enum.
    private static readonly Dictionary<int, string> TypeNames = new()
    {
        [0] = "Expense added",
        [1] = "Expense updated",
        [2] = "Expense deleted",
        [3] = "Comment added",
        [4] = "Added to group",
        [5] = "Removed from group",
        [8] = "Added as friend",
        [9] = "Removed as friend",
        [11] = "Debt simplification",
    };

    public static void RenderList(IReadOnlyList<Notification> notifications)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Date");
        table.AddColumn("Type");
        table.AddColumn("Content");

        foreach (var notification in notifications.OrderByDescending(n => n.CreatedAt))
        {
            table.AddRow(
                notification.CreatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-",
                TypeNames.GetValueOrDefault(notification.Type, $"Unknown ({notification.Type})"),
                (notification.Content ?? "-").EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }
}
