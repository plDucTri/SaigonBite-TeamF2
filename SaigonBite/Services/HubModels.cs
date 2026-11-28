namespace SaigonBite.Services;

/// <summary>One row of the Hubs and Capacity list (Figma A04). Load is (Confirmed + Preparing) / capacity (BR8).</summary>
public record HubSummary(
    int HubId,
    string Name,
    int KitchenCapacity,
    int Confirmed,
    int Preparing,
    decimal LoadPct,
    bool IsActive);

/// <summary>A kitchen staff member assigned to a hub. Staff are assigned on the account pages (UC2), not here.</summary>
public record HubStaffInfo(int UserId, string FullName, string Identifier);

/// <summary>A brand in the "Brands Operated" check list.</summary>
public record BrandOption(int BrandId, string Name, bool IsActive);

/// <summary>One hub as the Edit Hub page (Figma A09) needs it.</summary>
public record HubDetails(
    int HubId,
    string Name,
    string Address,
    int KitchenCapacity,
    bool IsActive,
    int Confirmed,
    int Preparing,
    decimal LoadPct,
    bool QuietDiscountEligible,
    IReadOnlyList<int> BrandIds,
    IReadOnlyList<string> BrandNames,
    IReadOnlyList<HubStaffInfo> Staff);

/// <summary>What the Admin typed in the hub form. KitchenCapacity is nullable so an empty box can be reported.</summary>
public class HubChanges
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int? KitchenCapacity { get; set; }
    public List<int> BrandIds { get; set; } = new();
    public bool IsActive { get; set; } = true;
}
