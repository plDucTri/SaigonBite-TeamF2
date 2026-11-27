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
