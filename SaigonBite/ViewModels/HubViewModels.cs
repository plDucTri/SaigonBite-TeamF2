using SaigonBite.Services;

namespace SaigonBite.ViewModels;

/// <summary>Add or Edit Hub (Figma A09). Id is null when a new hub is created.</summary>
public class HubFormViewModel
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public int? KitchenCapacity { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>The ticked brands of "Brands Operated".</summary>
    public List<int> BrandIds { get; set; } = new();

    // ----- shown only, filled by the controller -----
    public IReadOnlyList<BrandOption> BrandOptions { get; set; } = Array.Empty<BrandOption>();
    public IReadOnlyList<HubStaffInfo> Staff { get; set; } = Array.Empty<HubStaffInfo>();
    public int Confirmed { get; set; }
    public int Preparing { get; set; }
    public decimal LoadPct { get; set; }
    public bool QuietDiscountEligible { get; set; }

    public bool IsEdit => Id.HasValue;
}
