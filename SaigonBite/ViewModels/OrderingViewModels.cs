using SaigonBite.Services;
using SaigonBite.Services.Payments;

namespace SaigonBite.ViewModels;

public static class OrderLabel
{
    /// <summary>Order 1048 is shown as SB-1048 (Figma D05).</summary>
    public static string For(int orderId) => "SB-" + orderId;
}

// ---- D01 Browse menu ----------------------------------------------------------------------------
public class HubOptionViewModel
{
    public int HubId { get; set; }
    public string Name { get; set; } = "";
    public decimal LoadPct { get; set; }
    public bool IsBusy => LoadPct >= OrderValidator.BusyFromPct;       // BR8
    public bool IsQuiet => LoadPct < PricingEngine.QuietBelowPct;      // BR7
}

public class MenuItemRowViewModel
{
    public int ItemId { get; set; }
    public string Name { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
}

public class MenuViewModel
{
    public List<HubOptionViewModel> Hubs { get; set; } = new();
    public HubOptionViewModel? SelectedHub { get; set; }
    public List<MenuItemRowViewModel> Items { get; set; } = new();
    public int CartCount { get; set; }
}

// ---- D02 Cart -----------------------------------------------------------------------------------
public class CartLineViewModel
{
    public int ItemId { get; set; }
    public string Name { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class CartViewModel
{
    public int HubId { get; set; }
    public string HubName { get; set; } = "";
    public decimal HubLoadPct { get; set; }
    public List<CartLineViewModel> Lines { get; set; } = new();
    public decimal Subtotal => Lines.Sum(l => l.LineTotal);
}

// ---- D03 / D04 / D18 Checkout -------------------------------------------------------------------
/// <summary>The fields the customer fills in at checkout (posted back by the form).</summary>
public class CheckoutForm
{
    public string DeliveryAddress { get; set; } = "";
    public string? RoomNo { get; set; }          // Foreign Tourist: hotel room
    /// <summary>Typed as text and read with MoneyFormat.TryParseKm, so "3.2" means 3.2 in English and in Vietnamese.</summary>
    public string? Distance { get; set; }
    public string? VoucherCode { get; set; }
    public string? PaymentMethod { get; set; }
    public string Submit { get; set; } = "preview";   // "preview" recalculates, "place" places the order
}

public class CheckoutViewModel
{
    public CheckoutForm Form { get; set; } = new();
    public int HubId { get; set; }
    public string HubName { get; set; } = "";
    public decimal HubLoadPct { get; set; }
    public bool IsTourist { get; set; }
    public bool HeavyRain { get; set; }
    public PricingResult Pricing { get; set; } = new();
    public List<RuleViolation> Violations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public IReadOnlyList<PaymentOption> PaymentOptions { get; set; } = new List<PaymentOption>();
    public List<string> Messages { get; set; } = new();
}

// ---- D16 hub too busy, D17 order needs changes, D14 payment failed -----------------------------
public class HubBusyViewModel
{
    public string HubName { get; set; } = "";
    public decimal HubLoadPct { get; set; }
    public List<HubOptionViewModel> OtherHubs { get; set; } = new();
}

public class RejectedViewModel
{
    public List<RuleViolation> Violations { get; set; } = new();
}

// ---- D05 / D06 / D07 order details, tracking and history ---------------------------------------
public class OrderRowViewModel
{
    public int OrderId { get; set; }
    public string Label => OrderLabel.For(OrderId);
    public DateTime PlacedAt { get; set; }
    public string HubName { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public decimal Total { get; set; }
    public string Status { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
    public int? ShipperId { get; set; }
}

public class OrderLineRowViewModel
{
    public int ItemId { get; set; }
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount => UnitPrice * Quantity;
}

public class StatusHistoryRowViewModel
{
    public string? From { get; set; }
    public string To { get; set; } = "";
    public DateTime At { get; set; }
    public string By { get; set; } = "";
    public string? Reason { get; set; }
}

public class OrderDetailsViewModel
{
    public int OrderId { get; set; }
    public string Label => OrderLabel.For(OrderId);
    public string Status { get; set; } = "";
    public DateTime PlacedAt { get; set; }
    public string HubName { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string Address { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public int? ShipperId { get; set; }

    public List<OrderLineRowViewModel> Lines { get; set; } = new();
    public List<StatusHistoryRowViewModel> History { get; set; } = new();

    // Price breakdown stored with the order (BR10 order)
    public decimal Subtotal { get; set; }
    public decimal Quiet { get; set; }
    public decimal Voucher { get; set; }
    public string? VoucherCode { get; set; }
    public decimal Food => Subtotal - Quiet - Voucher;
    public decimal Delivery { get; set; }
    public decimal Rush { get; set; }
    public decimal Rain { get; set; }
    public decimal Total { get; set; }

    // Snapshot (BR17)
    public DateTime? SnapshotAt { get; set; }
    public decimal? SnapshotLoadPct { get; set; }
    public bool? SnapshotRain { get; set; }

    public string PaymentMethod { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
    public bool Refunded { get; set; }
    public bool IsCod => PaymentMethod == "COD";

    /// <summary>Statuses the viewer may move this order to (from OrderStateMachine); drives the buttons.</summary>
    public List<string> NextStatuses { get; set; } = new();
    public bool CanEdit { get; set; }
}

// ---- D08 edit items -----------------------------------------------------------------------------
public class EditOrderViewModel
{
    public int OrderId { get; set; }
    public string Label => OrderLabel.For(OrderId);
    public List<OrderLineRowViewModel> Lines { get; set; } = new();
    public List<MenuItemRowViewModel> AddableItems { get; set; } = new();
    public PricingResult Pricing { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public DateTime SnapshotAt { get; set; }
    public decimal SnapshotLoadPct { get; set; }
    public bool SnapshotRain { get; set; }
}

// ---- Admin: heavy-rain flag and gateway simulator ----------------------------------------------
public class SimulationViewModel
{
    public bool HeavyRain { get; set; }
    public bool GatewayDeclines { get; set; }
}
