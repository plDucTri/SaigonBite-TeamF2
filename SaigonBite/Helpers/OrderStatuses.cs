namespace SaigonBite.Helpers;

/// <summary>
/// Order status names stored in Orders.Status. The spelling is the same as the CHECK constraint
/// in Database/02_schema_orders.sql, so use these constants instead of typing the text.
/// </summary>
public static class OrderStatuses
{
    public const string Placed = "Placed";
    public const string Confirmed = "Confirmed";
    public const string Preparing = "Preparing";
    public const string Ready = "Ready";
    public const string OutForDelivery = "Out for Delivery";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";
    public const string Failed = "Failed";
}
