using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Repositories;

namespace SaigonBite.Services;

/// <summary>
/// Revenue Report R1. Services never touch the database: the orders come from IOrderRepository.
/// Register as scoped.
/// </summary>
public class ReportService : IReportService
{
    private const string NoCategory = "Uncategorised"; // an order whose lines have no usable category
    private const string UnknownMethod = "Unknown";    // an order without a payment row (should not happen)

    // Order statuses that count as "handled" by a kitchen in R2: the kitchen took the order on.
    // Placed (not accepted yet) and Cancelled are left out.
    private static readonly HashSet<string> HandledStatuses = new()
    {
        OrderStatuses.Confirmed, OrderStatuses.Preparing, OrderStatuses.Ready,
        OrderStatuses.OutForDelivery, OrderStatuses.Delivered, OrderStatuses.Failed
    };

    private readonly IOrderRepository _orders;
    private readonly IHubRepository _hubs;

    public ReportService(IOrderRepository orders, IHubRepository hubs)
    {
        _orders = orders;
        _hubs = hubs;
    }

    public ServiceResult<RevenueReportData> RevenueReport(DateOnly fromDate, DateOnly toDate)
    {
        // E6: invalid date range
        if (toDate < fromDate)
            return ServiceResult<RevenueReportData>.Fail("The end date must not be before the start date.");

        // From 00:00 of the first day to the very end of the last day.
        DateTime start = fromDate.ToDateTime(TimeOnly.MinValue);
        DateTime end = toDate.ToDateTime(TimeOnly.MaxValue);

        // Cancelled and Failed orders are already left out by the repository.
        IReadOnlyList<Order> orders = _orders.GetOrdersForRevenue(start, end);

        decimal included = 0;
        var byMethod = new Dictionary<string, decimal>();
        var byCategory = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (Order order in orders)
        {
            included += order.Total;

            // By payment method: the whole order total goes to the method it was paid with.
            string method = order.Payment?.Method ?? UnknownMethod;
            byMethod[method] = byMethod.GetValueOrDefault(method) + order.Total;

            // By category: the order total is shared between the categories of its lines.
            foreach (KeyValuePair<string, decimal> share in ShareByCategory(order))
                byCategory[share.Key] = byCategory.GetValueOrDefault(share.Key) + share.Value;
        }

        // All five methods are always listed (0 if not used); anything unexpected is added at the end.
        var methodRows = PaymentMethods.All
            .Select(m => new RevenueRow(m, byMethod.GetValueOrDefault(m)))
            .ToList();
        foreach (KeyValuePair<string, decimal> other in byMethod.Where(p => !PaymentMethods.All.Contains(p.Key)))
            methodRows.Add(new RevenueRow(other.Key, other.Value));

        var categoryRows = byCategory
            .Select(p => new RevenueRow(p.Key, p.Value))
            .OrderByDescending(r => r.Revenue)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ServiceResult<RevenueReportData>.Success(new RevenueReportData
        {
            FromDate = fromDate,
            ToDate = toDate,
            IncludedRevenue = included,
            OrderCount = orders.Count,
            ByCategory = categoryRows,
            ByPaymentMethod = methodRows
        });
    }

    public ServiceResult<HubUtilizationData> HubUtilizationReport(DateOnly fromDate, DateOnly toDate)
    {
        // E6: invalid date range
        if (toDate < fromDate)
            return ServiceResult<HubUtilizationData>.Fail("The end date must not be before the start date.");

        DateTime start = fromDate.ToDateTime(TimeOnly.MinValue);
        DateTime end = toDate.ToDateTime(TimeOnly.MaxValue);

        List<Hub> hubs = _hubs.GetActiveHubs();
        Dictionary<int, int> activeLoad = _hubs.CountActiveLoad();
        ILookup<int, HubOrderFact> handled = _hubs.GetOrderFacts(start, end)
            .Where(f => HandledStatuses.Contains(f.Status))
            .ToLookup(f => f.HubId);

        var rows = new List<HubUtilizationRow>();
        foreach (Hub hub in hubs)
        {
            List<HubOrderFact> facts = handled[hub.HubId].ToList();

            // Only orders that have both a Confirmed and a Ready time (and in that order) give a preparation time.
            List<double> minutes = facts
                .Where(f => f.ConfirmedAt.HasValue && f.ReadyAt.HasValue && f.ReadyAt >= f.ConfirmedAt)
                .Select(f => (f.ReadyAt!.Value - f.ConfirmedAt!.Value).TotalMinutes)
                .ToList();

            decimal load = hub.KitchenCapacity > 0
                ? Math.Round(activeLoad.GetValueOrDefault(hub.HubId) * 100m / hub.KitchenCapacity, 1)
                : 0m;

            rows.Add(new HubUtilizationRow
            {
                HubId = hub.HubId,
                HubName = hub.Name,
                OrdersHandled = facts.Count,
                AvgPrepMinutes = minutes.Count == 0 ? 0m : Math.Round((decimal)minutes.Average(), 1),
                CurrentLoadPct = load,
                QuietDiscounts = facts.Count(f => f.QuietDiscount > 0)
            });
        }

        rows = rows.OrderByDescending(r => r.OrdersHandled)
            .ThenBy(r => r.HubName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ServiceResult<HubUtilizationData>.Success(new HubUtilizationData
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalOrdersHandled = rows.Sum(r => r.OrdersHandled),
            TotalQuietDiscounts = rows.Sum(r => r.QuietDiscounts),
            ActiveHubCount = rows.Count,
            Hubs = rows
        });
    }

    /// <summary>
    /// Shares one order's Total between the categories of its lines, in proportion to the line amounts
    /// (UnitPrice x Quantity). The parts are whole VND and add up to the Total exactly:
    /// first every part is rounded down, then the VND that are left go one by one to the parts with
    /// the biggest fraction (largest remainder method).
    /// </summary>
    private static Dictionary<string, decimal> ShareByCategory(Order order)
    {
        // Line amount per category.
        var lineAmounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (OrderLine line in order.OrderLines)
        {
            string category = string.IsNullOrWhiteSpace(line.Item?.Category) ? NoCategory : line.Item!.Category.Trim();
            lineAmounts[category] = lineAmounts.GetValueOrDefault(category) + line.UnitPrice * line.Quantity;
        }

        decimal lineTotal = lineAmounts.Values.Sum();
        if (lineTotal <= 0)
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { [NoCategory] = order.Total };

        // Step 1: exact share, rounded down.
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var fractions = new List<(string Category, decimal Fraction)>();
        decimal given = 0;
        foreach (KeyValuePair<string, decimal> pair in lineAmounts)
        {
            decimal exact = order.Total * pair.Value / lineTotal;
            decimal whole = Math.Floor(exact);
            result[pair.Key] = whole;
            fractions.Add((pair.Key, exact - whole));
            given += whole;
        }

        // Step 2: hand out the VND that are left, biggest fraction first (same fraction: by name).
        int left = (int)(order.Total - given);
        foreach (var part in fractions
                     .OrderByDescending(f => f.Fraction)
                     .ThenBy(f => f.Category, StringComparer.OrdinalIgnoreCase)
                     .Take(left))
        {
            result[part.Category] += 1;
        }

        return result;
    }
}
