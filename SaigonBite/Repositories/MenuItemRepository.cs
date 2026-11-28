using Microsoft.EntityFrameworkCore;
using SaigonBite.Models.Entities;
using SaigonBite.Services;

namespace SaigonBite.Repositories;

public class MenuItemRepository : IMenuItemRepository
{
    private readonly SaigonBiteContext _db;

    public MenuItemRepository(SaigonBiteContext db)
    {
        _db = db;
    }

    public List<MenuItem> GetActive(MenuItemFilter filter)
    {
        IQueryable<MenuItem> query = _db.MenuItems
            .AsNoTracking()
            .Include(m => m.Brand)
            .Where(m => m.IsActive);

        if (filter.HubId is int hubId)
            query = query.Where(m => m.Brand.Hubs.Any(h => h.HubId == hubId));
        if (filter.BrandId is int brandId)
            query = query.Where(m => m.BrandId == brandId);
        if (!string.IsNullOrWhiteSpace(filter.Category))
            query = query.Where(m => m.Category == filter.Category);
        if (filter.Availability == AvailabilityFilter.Available)
            query = query.Where(m => m.IsAvailable);
        else if (filter.Availability == AvailabilityFilter.Unavailable)
            query = query.Where(m => !m.IsAvailable);

        return query.OrderBy(m => m.Brand.Name).ThenBy(m => m.Name).ToList();
    }

    public MenuItem? Find(int itemId) =>
        _db.MenuItems.Include(m => m.Brand).FirstOrDefault(m => m.ItemId == itemId);

    public bool NameExists(int brandId, string name, int? exceptItemId)
    {
        string text = name.Trim().ToLower();
        return _db.MenuItems.Any(m => m.BrandId == brandId && m.IsActive && m.Name.ToLower() == text
                                      && (exceptItemId == null || m.ItemId != exceptItemId));
    }

    public void Add(MenuItem item) => _db.MenuItems.Add(item);

    public void Save() => _db.SaveChanges();
}
