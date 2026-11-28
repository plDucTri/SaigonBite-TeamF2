using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Repositories;
using SaigonBite.Validators;

namespace SaigonBite.Services;

/// <summary>
/// Business layer of the Hubs and Capacity page. It checks the input with IHubValidator, applies the rules
/// that need the data and saves through the repositories. Register as scoped.
/// </summary>
public class HubService : IHubService
{
    private readonly IHubRepository _hubs;
    private readonly IBrandRepository _brands;
    private readonly IHubValidator _validator;

    public HubService(IHubRepository hubs, IBrandRepository brands, IHubValidator validator)
    {
        _hubs = hubs;
        _brands = brands;
        _validator = validator;
    }

    public IReadOnlyList<HubSummary> ListHubs()
    {
        Dictionary<int, HubOrderCounts> load = _hubs.CountLoadByStatus();
        return _hubs.GetAll().Select(h =>
        {
            HubOrderCounts l = load.GetValueOrDefault(h.HubId) ?? new HubOrderCounts(0, 0);
            return new HubSummary(h.HubId, h.Name, h.KitchenCapacity, l.Confirmed, l.Preparing,
                LoadPercent(l, h.KitchenCapacity), h.IsActive);
        }).ToList();
    }

    public ServiceResult<HubDetails> GetHub(int hubId)
    {
        Hub? hub = _hubs.Find(hubId);
        if (hub is null)
            return ServiceResult<HubDetails>.Fail("Hub not found.");

        HubOrderCounts load = _hubs.CountLoadByStatus().GetValueOrDefault(hubId) ?? new HubOrderCounts(0, 0);
        return ServiceResult<HubDetails>.Success(new HubDetails(
            hub.HubId, hub.Name, hub.Address, hub.KitchenCapacity, hub.IsActive,
            load.Confirmed, load.Preparing, LoadPercent(load, hub.KitchenCapacity),
            IsQuietEligible(load, hub.KitchenCapacity),
            hub.Brands.Select(b => b.BrandId).OrderBy(i => i).ToList(),
            hub.Brands.Select(b => b.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
            hub.Users.Where(u => u.Role == Roles.Kitchen && u.IsActive)
                .OrderBy(u => u.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(u => new HubStaffInfo(u.UserId, u.FullName, u.Identifier)).ToList()));
    }

    public ServiceResult<int> CreateHub(HubChanges changes)
    {
        var errors = _validator.ValidateHub(changes).ToList();
        if (errors.Count > 0)
            return ServiceResult<int>.Fail(errors);

        string name = changes.Name.Trim();
        if (_hubs.NameExists(name, null))
            return ServiceResult<int>.Fail("A hub with this name already exists.");

        List<int> brandIds = changes.BrandIds.Distinct().ToList();
        List<Brand> brands = _hubs.GetBrands(brandIds);
        if (brands.Count != brandIds.Count || brands.Any(b => !b.IsActive))
            return ServiceResult<int>.Fail("Choose active brands from the list.");

        var hub = new Hub
        {
            Name = name,
            Address = changes.Address.Trim(),
            KitchenCapacity = changes.KitchenCapacity!.Value,
            IsActive = true                       // a new hub always starts active
        };
        foreach (Brand brand in brands)
            hub.Brands.Add(brand);

        _hubs.Add(hub);
        _hubs.Save();
        return ServiceResult<int>.Success(hub.HubId);
    }

    public OperationResult UpdateHub(int hubId, HubChanges changes)
    {
        Hub? hub = _hubs.Find(hubId);
        if (hub is null)
            return OperationResult.Fail("Hub not found.");

        var errors = _validator.ValidateHub(changes).ToList();
        if (errors.Count > 0)
            return OperationResult.Fail(errors.ToArray());

        string name = changes.Name.Trim();
        if (_hubs.NameExists(name, hubId))
            return OperationResult.Fail("A hub with this name already exists.");

        // Brands the hub already operates may stay even if they are inactive now; a newly added brand must be active.
        List<int> wanted = changes.BrandIds.Distinct().ToList();
        List<int> current = hub.Brands.Select(b => b.BrandId).ToList();
        List<int> added = wanted.Except(current).ToList();
        List<Brand> addedBrands = _hubs.GetBrands(added);
        if (addedBrands.Count != added.Count || addedBrands.Any(b => !b.IsActive))
            return OperationResult.Fail("Choose active brands from the list.");

        if (hub.IsActive && !changes.IsActive && _hubs.HasOpenOrders(hubId))
            return OperationResult.Fail("This hub still has orders in progress, so it cannot be deactivated.");

        hub.Name = name;
        hub.Address = changes.Address.Trim();
        hub.KitchenCapacity = changes.KitchenCapacity!.Value;   // a lower capacity only affects new orders (BR8)
        hub.IsActive = changes.IsActive;

        foreach (Brand removed in hub.Brands.Where(b => !wanted.Contains(b.BrandId)).ToList())
            hub.Brands.Remove(removed);                         // open orders keep their lines; only new orders are affected
        foreach (Brand brand in addedBrands)
            hub.Brands.Add(brand);

        _hubs.Save();
        return OperationResult.Ok();
    }

    public OperationResult DeactivateHub(int hubId)
    {
        Hub? hub = _hubs.Find(hubId);
        if (hub is null)
            return OperationResult.Fail("Hub not found.");
        if (!hub.IsActive)
            return OperationResult.Fail("This hub is already inactive.");
        if (_hubs.HasOpenOrders(hubId))
            return OperationResult.Fail("This hub still has orders in progress, so it cannot be deactivated.");

        hub.IsActive = false;
        _hubs.Save();
        return OperationResult.Ok();
    }

    public IReadOnlyList<BrandOption> ListBrandOptions(IEnumerable<int>? keepBrandIds)
    {
        HashSet<int> keep = keepBrandIds?.ToHashSet() ?? new HashSet<int>();
        return _brands.GetAll(null)
            .Where(b => b.IsActive || keep.Contains(b.BrandId))
            .Select(b => new BrandOption(b.BrandId, b.Name, b.IsActive))
            .ToList();
    }

    // ================= Load (BR7, BR8) =================

    private static decimal LoadPercent(HubOrderCounts load, int capacity) =>
        capacity > 0 ? Math.Round((load.Confirmed + load.Preparing) * 100m / capacity, 1) : 0m;

    // BR7: a Quiet Kitchen discount is given while the load is below the threshold (30%, PricingEngine.QuietBelowPct).
    // Compared without rounding, so 29.96% is still "below 30%".
    private static bool IsQuietEligible(HubOrderCounts load, int capacity) =>
        capacity > 0 && (load.Confirmed + load.Preparing) * 100m < PricingEngine.QuietBelowPct * capacity;
}
