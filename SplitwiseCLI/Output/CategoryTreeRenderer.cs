using Spectre.Console;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Output;

public static class CategoryTreeRenderer
{
    public static void Render(IReadOnlyList<Category> categories)
    {
        var tree = new Tree("Categories");

        foreach (var category in categories.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            var node = tree.AddNode(category.Name.EscapeMarkup());
            foreach (var subcategory in category.Subcategories.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            {
                node.AddNode($"{subcategory.Name.EscapeMarkup()} [grey](id: {subcategory.Id})[/]");
            }
        }

        AnsiConsole.Write(tree);
    }
}
