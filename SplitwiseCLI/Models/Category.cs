using System.Text.Json.Serialization;

namespace SplitwiseCLI.Models;

public sealed class CategoriesResponse
{
    [JsonPropertyName("categories")]
    public List<Category> Categories { get; init; } = [];
}

public sealed class Category
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("subcategories")]
    public List<Subcategory> Subcategories { get; init; } = [];
}

public sealed class Subcategory
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
