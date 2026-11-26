namespace SaigonBite.Services;

/// <summary>The eight order states of BR15. Values match the CHECK constraint on Orders.Status.</summary>
public static class OrderStatus
{
    public const string Placed = "Placed";
    public const string Confirmed = "Confirmed";
    public const string Preparing = "Preparing";
    public const string Ready = "Ready";
    public const string OutForDelivery = "Out for Delivery";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";
    public const string Failed = "Failed";

    public static readonly string[] All =
    {
        Placed, Confirmed, Preparing, Ready, OutForDelivery, Delivered, Cancelled, Failed
    };

    public static bool IsTerminal(string status) =>
        status == Delivered || status == Cancelled || status == Failed;

    /// <summary>Bootstrap badge class used by the views.</summary>
    public static string Badge(string status) => status switch
    {
        Placed => "bg-primary",
        Confirmed => "bg-info text-dark",
        Preparing => "bg-warning text-dark",
        Ready => "bg-success",
        OutForDelivery => "bg-warning text-dark",
        Delivered => "bg-success",
        Cancelled => "bg-danger",
        Failed => "bg-danger",
        _ => "bg-secondary"
    };
}

/// <summary>Values stored in Payments.Status (NVARCHAR(20)).</summary>
public static class PaymentStatus
{
    public const string Paid = "Paid";                   // online payment (simulated) succeeded
    public const string Pending = "Pending";             // COD, cash not collected yet
    public const string CashCollected = "CashCollected"; // COD, the shipper confirmed the cash (BR14)
    public const string Refunded = "Refunded";           // online payment refunded (BR16, A-02)
    public const string Voided = "Voided";               // COD order cancelled or failed, nothing was collected
}
