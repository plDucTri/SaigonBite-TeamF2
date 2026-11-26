using Microsoft.EntityFrameworkCore;
using SaigonBite.Helpers;
using SaigonBite.Models.Entities;

namespace SaigonBite.Repositories;

public class OrderCatalogRepository : IOrderCatalogRepository
{
    private readonly SaigonBiteContext _db;

    public OrderCatalogRepository(SaigonBiteContext db)
    {
        _db = db;
    }

    public Task<Hub?> GetHubWithBrandsAsync(int hubId) =>
        _db.Hubs.Include(h => h.Brands).FirstOrDefaultAsync(h => h.HubId == hubId);

    public Task<List<Hub>> GetActiveHubsAsync() =>
        _db.Hubs.Where(h => h.IsActive).OrderBy(h => h.Name).ToListAsync();

    public Task<List<MenuItem>> GetMenuItemsAsync(IEnumerable<int> itemIds)
    {
        var ids = itemIds.Distinct().ToList();
        return _db.MenuItems.Include(m => m.Brand).Where(m => ids.Contains(m.ItemId)).ToListAsync();
    }

    public Task<List<MenuItem>> GetHubMenuAsync(int hubId) =>
        _db.MenuItems
            .Include(m => m.Brand)
            .Where(m => m.IsActive && m.IsAvailable && m.Brand.IsActive &&
                        m.Brand.Hubs.Any(h => h.HubId == hubId))
            .OrderBy(m => m.Brand.Name).ThenBy(m => m.Name)
            .ToListAsync();

    public Task<List<User>> GetActiveShippersAsync() =>
        _db.Users.Where(u => u.IsActive && u.Role == Roles.Shipper).OrderBy(u => u.UserId).ToListAsync();
}
