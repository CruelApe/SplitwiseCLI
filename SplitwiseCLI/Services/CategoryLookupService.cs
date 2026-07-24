using SplitwiseCLI.Api;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Services;

public sealed record CategoryLookup(IReadOnlyList<Category> Categories, IReadOnlyDictionary<long, string> SubcategoryNamesById);

public sealed class CategoryLookupService(ISplitwiseClient client)
{
    public async Task<CategoryLookup> LoadAsync(CancellationToken cancellationToken = default)
    {
        var categories = await client.GetCategoriesAsync(cancellationToken);

        var namesById = new Dictionary<long, string>();
        foreach (var subcategory in categories.SelectMany(c => c.Subcategories))
        {
            namesById[subcategory.Id] = subcategory.Name;
        }

        return new CategoryLookup(categories, namesById);
    }
}
