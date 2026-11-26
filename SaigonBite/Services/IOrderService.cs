using SaigonBite.Models.Entities;
using SaigonBite.Services.Payments;

namespace SaigonBite.Services;

/// <summary>What the customer typed at checkout (FR1 to FR7). Lines are the cart.</summary>
public class CheckoutRequest
{
    public int HubId { get; set; }
    public List<LineRequest> Lines { get; set; } = new();
    public decimal DistanceKm { get; set; }
    public string DeliveryAddress { get; set; } = "";
    /// <summary>One voucher code. If several are typed (separated by comma, semicolon or space) only the first is used (BR9, T-17).</summary>
    public string? VoucherCode { get; set; }
    public string PaymentMethod { get; set; } = "";
}

/// <summary>The live price shown at checkout. Nothing is saved.</summary>
public class CheckoutPreview
{
    public string CustomerRole { get; set; } = "";
    public string HubName { get; set; } = "";
    public decimal HubLoadPct { get; set; }
    public bool HeavyRain { get; set; }
    public PricingResult Pricing { get; set; } = new();
    public List<RuleViolation> Violations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public IReadOnlyList<PaymentOption> PaymentOptions { get; set; } = new List<PaymentOption>();
}

public class OrderOutcome
{
    public bool Success { get; set; }
    public int OrderId { get; set; }
    public List<RuleViolation> Violations { get; } = new();
    /// <summary>Voucher refused, second voucher ignored: the order is still placed.</summary>
    public List<string> Warnings { get; } = new();
    public bool PaymentFailed { get; set; }
    public string Message { get; set; } = "";
    public bool HubTooBusy => Violations.Any(v => v.Rule == "BR8");
    public int HubId { get; set; }
    public decimal HubLoadPct { get; set; }
}

/// <summary>Result of checking an edit (BR17): the recalculated price, or the rules the edit breaks.</summary>
public class EditEvaluation
{
    public Order? Order { get; set; }
    public PricingResult Pricing { get; set; } = new();
    public List<RuleViolation> Violations { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<EditLine> Lines { get; } = new();
    public bool Success => Order != null && Violations.Count == 0;
}

public record EditLine(MenuItem Item, int Quantity, decimal UnitPrice);

public record HubLoad(Hub Hub, decimal LoadPct);

/// <summary>Business layer of UC1 Place and Manage Orders (CRC card OrderService).</summary>
public interface IOrderService
{
    // Catalogue reads for the ordering screens
    Task<List<HubLoad>> GetHubLoadsAsync();
    Task<decimal> GetHubLoadPctAsync(int hubId);
    Task<Hub?> GetHubAsync(int hubId);
    Task<List<MenuItem>> GetMenuAsync(int hubId);
    Task<List<MenuItem>> GetItemsAsync(IEnumerable<int> itemIds);

    // Place (FR1 to FR7)
    Task<CheckoutPreview?> PreviewAsync(int customerId, CheckoutRequest request);
    Task<OrderOutcome> PlaceOrderAsync(int customerId, CheckoutRequest request);

    // Edit while Placed (FR11, BR17)
    Task<EditEvaluation> EvaluateEditAsync(int orderId, int customerId, IEnumerable<LineRequest> lines);
    Task<OperationResult> EditOrderAsync(int orderId, int customerId, IEnumerable<LineRequest> lines);

    // Lifecycle (FR8, FR10, BR15, BR16)
    Task<OperationResult> CancelAsync(int orderId, int actorId, string? reason = null);
    Task<OperationResult> ChangeStatusAsync(int orderId, int actorId, string toStatus, string? reason = null);
    Task<OperationResult> AcceptJobAsync(int orderId, int shipperId);
    Task<OperationResult> ConfirmCashAsync(int orderId, int shipperId);

    /// <summary>A-01: gives Ready orders that nobody accepted for 5 minutes to the least busy shipper. Returns how many were assigned.</summary>
    Task<int> AssignOverdueJobsAsync();

    // Track (FR9, NFR5)
    Task<Order?> GetOrderAsync(int orderId, int viewerId);
    Task<List<Order>> TrackAsync(int customerId, string? status, DateTime? from, DateTime? to);
    Task<List<Order>> GetKitchenQueueAsync(int kitchenUserId);
    Task<List<Order>> GetReadyJobsAsync();
    Task<List<Order>> GetShipperOrdersAsync(int shipperId);
    Task<List<Order>> GetAllOrdersAsync(string? status, int? hubId);
}
