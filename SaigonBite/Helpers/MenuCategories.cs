namespace SaigonBite.Helpers;

/// <summary>
/// The categories a menu item can have (UC3, FR15). Revenue Report R1 groups its revenue by these names,
/// so the Admin picks one from the list instead of typing free text (test T-50: "a category not in the list").
/// </summary>
public static class MenuCategories
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "Noodles", "Rice", "Sandwiches", "Snacks", "Desserts", "Drinks"
    };

    /// <summary>The category as it is spelled in the list (any letter case is accepted), or null if it is not in the list.</summary>
    public static string? Find(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return null;
        string text = category.Trim();
        return All.FirstOrDefault(c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase));
    }
}
