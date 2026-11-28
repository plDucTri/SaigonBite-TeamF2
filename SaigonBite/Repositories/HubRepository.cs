using Microsoft.EntityFrameworkCore;
using SaigonBite.Helpers;
using SaigonBite.Models.Entities;

namespace SaigonBite.Repositories;

public class HubRepository : IHubRepository
{
    private readonly SaigonBiteContext _db;

    public HubRepository(SaigonBiteContext db)
    {
        _db = db;
    }

    public List<Hub> GetActiveHubs() =>
        _db.Hubs.AsNoTracking().Where(h => h.IsActive).OrderBy(h => h.Name).ToList();

    public Dictionary<int, int> CountActiveLoad() =>
        _db.Orders
            .Where(o => o.Status == OrderStatuses.Confirmed || o.Status == OrderStatuses.Preparing)
            .GroupBy(o => o.HubId)
            .Select(g => new { HubId = g.Key, Count = g.Count() })
            .ToDictionary(x => x.HubId, x => x.Count);

    public List<HubOrderFact> GetOrderFacts(DateTime start, DateTime end) =>
        _db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= start && o.CreatedAt <= end)
            .Select(o => new
            {
                o.HubId,
                o.Status,
                o.QuietDiscountQ,
                ConfirmedAt = o.OrderStatusHistories
                    .Where(h => h.ToStatus == OrderStatuses.Confirmed)
                    .Min(h => (DateTime?)h.ChangedAt),
                ReadyAt = o.OrderStatusHistories
                    .Where(h => h.ToStatus == OrderStatuses.Ready)
                    .Min(h => (DateTime?)h.ChangedAt)
            })
            .ToList()
            .Select(x => new HubOrderFact(x.HubId, x.Status, x.QuietDiscountQ, x.ConfirmedAt, x.ReadyAt))
            .ToList();

    // ----- Hubs and Capacity page (FR17) -----

    public List<Hub> GetAll() =>
        _db.Hubs.AsNoTracking().OrderBy(h => h.Name).ToList();

    public Dictionary<int, HubOrderCounts> CountLoadByStatus() =>
        _db.Orders
            .Where(o => o.Status == OrderStatuses.Confirmed || o.Status == OrderStatuses.Preparing)
            .GroupBy(o => o.HubId)
            .Select(g => new
            {
                HubId = g.Key,
                Confirmed = g.Count(o => o.Status == OrderStatuses.Confirmed),
                Preparing = g.Count(o => o.Status == OrderStatuses.Preparing)
            })
            .ToDictionary(x => x.HubId, x => new HubOrderCounts(x.Confirmed, x.Preparing));

    public Hub? Find(int hubId) =>
        _db.Hubs
            .Include(h => h.Brands)
            .Include(h => h.Users)
            .FirstOrDefault(h => h.HubId == hubId);

    public bool NameExists(string name, int? exceptHubId)
    {
        string text = name.Trim().ToLower();
        return _db.Hubs.Any(h => h.Name.ToLower() == text && (exceptHubId == null || h.HubId != exceptHubId));
    }

    public bool HasOpenOrders(int hubId) =>
        _db.Orders.Any(o => o.HubId == hubId
                            && o.Status != OrderStatuses.Delivered
                            && o.Status != OrderStatuses.Cancelled
                            && o.Status != OrderStatuses.Failed);

    public List<Brand> GetBrands(IEnumerable<int> brandIds)
    {
        List<int> ids = brandIds.Distinct().ToList();
        return _db.Brands.Where(b => ids.Contains(b.BrandId)).ToList();
    }

    public void Add(Hub hub) => _db.Hubs.Add(hub);

    public void Save() => _db.SaveChanges();
}
