using SplitwiseCLI.Api;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Services;

public sealed record CategoryLookup(IReadOnlyList<Category> Categories, IReadOnlyDictionary<string, long> SubcategoryIdsByName);

public sealed class CategoryLookupService(ISplitwiseClient client)
{
    public async Task<CategoryLookup> LoadAsync(CancellationToken cancellationToken = default)
    {
        var categories = await client.GetCategoriesAsync(cancellationToken);

        var byName = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var subcategory in categories.SelectMany(c => c.Subcategories))
        {
            byName[subcategory.Name] = subcategory.Id;
        }

        return new CategoryLookup(categories, byName);
    }
}
