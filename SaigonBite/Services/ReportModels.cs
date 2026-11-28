namespace SaigonBite.Services;

/// <summary>One line of a report table: a category or a payment method and its revenue in VND.</summary>
public record RevenueRow(string Name, decimal Revenue);

/// <summary>What Revenue Report R1 shows. Both tables add up to IncludedRevenue.</summary>
public class RevenueReportData
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    /// <summary>Sum of Orders.Total of the orders that are not Cancelled or Failed.</summary>
    public decimal IncludedRevenue { get; set; }

    public int OrderCount { get; set; }

    /// <summary>Only categories that have revenue, biggest first.</summary>
    public IReadOnlyList<RevenueRow> ByCategory { get; set; } = Array.Empty<RevenueRow>();

    /// <summary>All five payment methods in a fixed order, 0 when a method was not used.</summary>
    public IReadOnlyList<RevenueRow> ByPaymentMethod { get; set; } = Array.Empty<RevenueRow>();
}

/// <summary>One hub in the Hub Utilization Report R2 (Figma A07).</summary>
public class HubUtilizationRow
{
    public int HubId { get; set; }
    public string HubName { get; set; } = string.Empty;

    /// <summary>Orders of the date range that the kitchen took on: every status except Placed and Cancelled.</summary>
    public int OrdersHandled { get; set; }

    /// <summary>Average minutes from Confirmed to Ready. 0 when no order of the range has both times.</summary>
    public decimal AvgPrepMinutes { get; set; }

    /// <summary>Right now (not tied to the range): Confirmed + Preparing orders divided by the kitchen capacity (BR8).</summary>
    public decimal CurrentLoadPct { get; set; }

    /// <summary>Handled orders of the range that got the Quiet Kitchen discount (BR7).</summary>
    public int QuietDiscounts { get; set; }
}

/// <summary>What Hub Utilization Report R2 shows. The totals add up the rows.</summary>
public class HubUtilizationData
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int TotalOrdersHandled { get; set; }
    public int TotalQuietDiscounts { get; set; }
    public int ActiveHubCount { get; set; }

    /// <summary>Every active hub (also one without orders, with 0s), most orders handled first.</summary>
    public IReadOnlyList<HubUtilizationRow> Hubs { get; set; } = Array.Empty<HubUtilizationRow>();
}
