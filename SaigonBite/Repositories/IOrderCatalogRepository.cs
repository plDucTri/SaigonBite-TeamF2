using SaigonBite.Models.Entities;

namespace SaigonBite.Repositories;

/// <summary>
/// Read-only lookups that UC1 needs from data owned by other use cases (hubs, brands and menu items, shippers).
/// Kept apart from their repositories so the use cases do not edit each other's classes; when UC3 publishes
/// HubRepository and MenuItemRepository you may switch to them.
/// </summary>
public interface IOrderCatalogRepository
{
    Task<Hub?> GetHubWithBrandsAsync(int hubId);
    Task<List<Hub>> GetActiveHubsAsync();

    /// <summary>Items by id, with their brand.</summary>
    Task<List<MenuItem>> GetMenuItemsAsync(IEnumerable<int> itemIds);

    /// <summary>Active, Available items of the active brands the hub operates (BR12).</summary>
    Task<List<MenuItem>> GetHubMenuAsync(int hubId);

    /// <summary>Active users with the Shipper role (A-01 automatic assignment).</summary>
    Task<List<User>> GetActiveShippersAsync();
}
