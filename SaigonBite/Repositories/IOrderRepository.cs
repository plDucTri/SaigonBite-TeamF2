using SaigonBite.Models.Entities;

namespace SaigonBite.Repositories;

/// <summary>How busy a shipper is (A-01 automatic assignment).</summary>
public record ShipperActivity(int ActiveDeliveries, DateTime? LastActivity);

/// <summary>Data access for orders (UC1). No business rules here: only queries and saving.</summary>
public interface IOrderRepository
{
    /// <summary>Order with lines (and items), snapshot, payment, status history (and who changed it), hub and customer.</summary>
    Task<Order?> GetWithDetailsAsync(int orderId);

    Task<List<Order>> GetByCustomerAsync(int customerId, string? status, DateTime? from, DateTime? to);
    Task<List<Order>> GetByHubAsync(int hubId);
    Task<List<Order>> GetByStatusAsync(string status);
    Task<List<Order>> GetByShipperAsync(int shipperId);
    Task<List<Order>> GetAllAsync(string? status, int? hubId);

    /// <summary>BR8: number of orders at the hub that are Confirmed or Preparing (the new order is not counted).</summary>
    Task<int> CountActiveLoadAsync(int hubId);

    /// <summary>A-01: Ready orders nobody accepted that became Ready at or before the cutoff time.</summary>
    Task<List<Order>> GetOverdueReadyAsync(DateTime readyBefore);

    /// <summary>A-01: for each shipper, the active deliveries (Ready or Out for Delivery) and the last status change of their orders.</summary>
    Task<Dictionary<int, ShipperActivity>> GetShipperActivityAsync();

    Task AddAsync(Order order);

    /// <summary>Marks one order line for deletion (used when an edit removes an item while the order is Placed).</summary>
    void RemoveLine(OrderLine line);

    Task SaveAsync();
}
