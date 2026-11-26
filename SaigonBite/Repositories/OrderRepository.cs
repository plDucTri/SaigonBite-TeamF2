using Microsoft.EntityFrameworkCore;
using SaigonBite.Models.Entities;
using SaigonBite.Services;

namespace SaigonBite.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly SaigonBiteContext _db;

    public OrderRepository(SaigonBiteContext db)
    {
        _db = db;
    }

    public Task<Order?> GetWithDetailsAsync(int orderId) =>
        _db.Orders
            .Include(o => o.OrderLines).ThenInclude(l => l.Item)
            .Include(o => o.PriceSnapshot)
            .Include(o => o.Payment)
            .Include(o => o.OrderStatusHistories).ThenInclude(h => h.ChangedByNavigation)
            .Include(o => o.Hub).ThenInclude(h => h.Brands)
            .Include(o => o.Customer)
            .Include(o => o.VoucherCodeNavigation)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

    public Task<List<Order>> GetByCustomerAsync(int customerId, string? status, DateTime? from, DateTime? to)
    {
        var q = ListQuery().Where(o => o.CustomerId == customerId);
        if (!string.IsNullOrEmpty(status)) q = q.Where(o => o.Status == status);
        if (from.HasValue) q = q.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(o => o.CreatedAt < to.Value.AddDays(1));
        return q.OrderByDescending(o => o.CreatedAt).ToListAsync();
    }

    public Task<List<Order>> GetByHubAsync(int hubId) =>
        ListQuery().Where(o => o.HubId == hubId).OrderByDescending(o => o.CreatedAt).ToListAsync();

    public Task<List<Order>> GetByStatusAsync(string status) =>
        ListQuery().Where(o => o.Status == status).OrderBy(o => o.CreatedAt).ToListAsync();

    public Task<List<Order>> GetByShipperAsync(int shipperId) =>
        ListQuery().Where(o => o.ShipperId == shipperId).OrderByDescending(o => o.CreatedAt).ToListAsync();

    public Task<List<Order>> GetAllAsync(string? status, int? hubId)
    {
        var q = ListQuery();
        if (!string.IsNullOrEmpty(status)) q = q.Where(o => o.Status == status);
        if (hubId.HasValue) q = q.Where(o => o.HubId == hubId.Value);
        return q.OrderByDescending(o => o.CreatedAt).ToListAsync();
    }

    public Task<int> CountActiveLoadAsync(int hubId) =>
        _db.Orders.CountAsync(o => o.HubId == hubId &&
                                   (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Preparing));

    public Task<List<Order>> GetOverdueReadyAsync(DateTime readyBefore) =>
        _db.Orders
            .Where(o => o.Status == OrderStatus.Ready && o.ShipperId == null &&
                        o.OrderStatusHistories.Any(h => h.ToStatus == OrderStatus.Ready && h.ChangedAt <= readyBefore))
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

    public async Task<Dictionary<int, ShipperActivity>> GetShipperActivityAsync()
    {
        var active = await _db.Orders
            .Where(o => o.ShipperId != null && (o.Status == OrderStatus.Ready || o.Status == OrderStatus.OutForDelivery))
            .GroupBy(o => o.ShipperId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync();

        var last = await _db.OrderStatusHistories
            .Where(h => h.Order.ShipperId != null)
            .GroupBy(h => h.Order.ShipperId!.Value)
            .Select(g => new { Id = g.Key, Last = g.Max(h => h.ChangedAt) })
            .ToListAsync();

        var result = new Dictionary<int, ShipperActivity>();
        foreach (var a in active)
            result[a.Id] = new ShipperActivity(a.Count, null);
        foreach (var l in last)
            result[l.Id] = new ShipperActivity(result.TryGetValue(l.Id, out var x) ? x.ActiveDeliveries : 0, l.Last);
        return result;
    }

    public async Task AddAsync(Order order) => await _db.Orders.AddAsync(order);

    public void RemoveLine(OrderLine line) => _db.OrderLines.Remove(line);

    public Task SaveAsync() => _db.SaveChangesAsync();

    // Light query for list screens.
    private IQueryable<Order> ListQuery() =>
        _db.Orders
            .Include(o => o.Hub)
            .Include(o => o.Customer)
            .Include(o => o.Payment)
            .Include(o => o.OrderLines);
}
