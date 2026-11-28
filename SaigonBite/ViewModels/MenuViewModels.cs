using SaigonBite.Services;

namespace SaigonBite.ViewModels;

/// <summary>Brands screen (Figma A01): the search box and the rows.</summary>
public class BrandListViewModel
{
    public string? Search { get; set; }
    public IReadOnlyList<BrandInfo> Brands { get; set; } = Array.Empty<BrandInfo>();
}

/// <summary>Add or Edit Brand (Figma A10). Id is null when a new brand is created.</summary>
public class BrandFormViewModel
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Read-only text: the hubs are chosen in the hub editor, not here.</summary>
    public IReadOnlyList<string> HubNames { get; set; } = Array.Empty<string>();

    public bool IsEdit => Id.HasValue;
}

/// <summary>Menu Items screen (Figma A02): the four filters, the drop-down lists and the rows.</summary>
public class MenuItemListViewModel
{
    public int? HubId { get; set; }
    public int? BrandId { get; set; }
    public string? Category { get; set; }
    public string? Availability { get; set; }

    public IReadOnlyList<HubOption> Hubs { get; set; } = Array.Empty<HubOption>();
    public IReadOnlyList<BrandInfo> Brands { get; set; } = Array.Empty<BrandInfo>();
    public IReadOnlyList<MenuItemInfo> Items { get; set; } = Array.Empty<MenuItemInfo>();
}

/// <summary>Add or Edit Menu Item (Figma A03). Id is null when a new item is created.</summary>
public class MenuItemFormViewModel
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public int BrandId { get; set; }
    public string? Category { get; set; }
    public decimal? Price { get; set; }
    public bool IsAvailable { get; set; } = true;

    /// <summary>Brands for the drop-down (the active ones, plus the current brand of an item that is being edited).</summary>
    public IReadOnlyList<BrandInfo> Brands { get; set; } = Array.Empty<BrandInfo>();

    public bool IsEdit => Id.HasValue;
}

/// <summary>Hub Utilization Report R2: the date boxes and, when the range is valid, the report.</summary>
public class HubUtilizationViewModel
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    /// <summary>Null when the range was not valid. The reason is in ModelState.</summary>
    public HubUtilizationData? Report { get; set; }
}
