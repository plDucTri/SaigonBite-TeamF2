namespace SaigonBite.Services;

// Hub drop-down items use the existing HubOption record from AccountModels.cs.

/// <summary>One row of the Brands screen (Figma A01). ItemCount counts the items that are not removed.</summary>
public record BrandInfo(int BrandId, string Name, bool IsActive, int ItemCount, IReadOnlyList<string> HubNames);

/// <summary>One row of the Menu Items screen (Figma A02).</summary>
public record MenuItemInfo(
    int ItemId,
    int BrandId,
    string BrandName,
    bool BrandIsActive,
    string Name,
    string Category,
    decimal Price,
    bool IsAvailable,
    bool IsActive);

/// <summary>What the Admin typed in the brand form (Figma A10).</summary>
public class BrandChanges
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>What the Admin typed in the menu item form (Figma A03). Price is nullable so an empty box can be reported.</summary>
public class MenuItemChanges
{
    public string Name { get; set; } = string.Empty;
    public int BrandId { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public bool IsAvailable { get; set; } = true;
}

/// <summary>The four filters of the Menu Items screen. A null value means "all".</summary>
public class MenuItemFilter
{
    public int? HubId { get; set; }
    public int? BrandId { get; set; }
    public string? Category { get; set; }

    /// <summary>"available", "unavailable" or null for any.</summary>
    public string? Availability { get; set; }
}

public static class AvailabilityFilter
{
    public const string Available = "available";
    public const string Unavailable = "unavailable";
}
