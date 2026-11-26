using System.Globalization;
using SaigonBite.Models.Entities;

namespace SaigonBite.Services;

/// <summary>Everything OrderValidator needs to check one order. No database access: the caller supplies the facts.</summary>
public class OrderCheck
{
    /// <summary>Item subtotal S before any discount (BR10 step 1).</summary>
    public decimal Subtotal { get; set; }
    public decimal DistanceKm { get; set; }
    /// <summary>Hub load in percent (0 to 100); the order being checked is not counted (BR8).</summary>
    public decimal HubLoadPct { get; set; }

    /// <summary>Lines after merging duplicates (one line per item).</summary>
    public IReadOnlyList<LineRequest> Lines { get; set; } = new List<LineRequest>();
    /// <summary>Menu items found in the database, by ItemId.</summary>
    public IReadOnlyDictionary<int, MenuItem> Items { get; set; } = new Dictionary<int, MenuItem>();
    /// <summary>Ids of the active brands the chosen hub operates.</summary>
    public ISet<int> HubBrandIds { get; set; } = new HashSet<int>();

    /// <summary>True when a new order is being placed. BR8 protects NEW orders only, so an edit sets this to false (A-10).</summary>
    public bool IsNewOrder { get; set; } = true;
}

public interface IOrderValidator
{
    /// <summary>Checks BR1, BR2, BR8 and BR12 and returns every violation (empty list = valid).</summary>
    List<RuleViolation> Validate(OrderCheck check);
}

/// <summary>
/// CRC card OrderValidator (report 2.4): BR1 minimum subtotal, BR2 coverage, BR8 hub protection and
/// BR12 order lines. Pure rules, so the EP/BVA tests need no database.
/// </summary>
public class OrderValidator : IOrderValidator
{
    public const decimal MinSubtotal = 50_000m;   // BR1
    public const decimal MaxDistanceKm = 8.0m;    // BR2
    public const decimal BusyFromPct = 90m;       // BR8
    public const int MinQuantity = 1;             // BR12
    public const int MaxQuantity = 20;
    public const int MinLines = 1;
    public const int MaxLines = 30;

    public List<RuleViolation> Validate(OrderCheck c)
    {
        var list = new List<RuleViolation>();

        // BR12: lines, quantities, availability, brands of the hub (FR1)
        if (c.Lines.Count < MinLines)
            list.Add(new RuleViolation("BR12", "The order has no items.", "Add at least one item."));
        if (c.Lines.Count > MaxLines)
            list.Add(new RuleViolation("BR12", $"The order has {c.Lines.Count} lines; the maximum is {MaxLines}.", "Remove some lines."));

        foreach (var line in c.Lines)
        {
            if (!c.Items.TryGetValue(line.ItemId, out var item) || !item.IsActive)
            {
                list.Add(new RuleViolation("BR12", $"Item #{line.ItemId} does not exist.", "Remove it from the cart."));
                continue;
            }
            if (line.Quantity < MinQuantity || line.Quantity > MaxQuantity)
                list.Add(new RuleViolation("BR12",
                    $"{item.Name} quantity is {line.Quantity} (maximum {MaxQuantity} per line).",
                    $"Use a quantity from {MinQuantity} to {MaxQuantity}, or remove the item."));
            if (!item.IsAvailable)
                list.Add(new RuleViolation("BR12", $"{item.Name} is unavailable.", "Remove it from the cart."));
            if (!c.HubBrandIds.Contains(item.BrandId))
                list.Add(new RuleViolation("BR12", $"{item.Name} is not sold by this hub.", "Order it from a hub that operates its brand."));
        }

        // BR1 minimum subtotal (FR1)
        if (c.Subtotal < MinSubtotal)
            list.Add(new RuleViolation("BR1",
                $"Item subtotal is {Vnd(c.Subtotal)}; the minimum is {Vnd(MinSubtotal)}.",
                $"Add items worth at least {Vnd(MinSubtotal - c.Subtotal)} more."));

        // BR2 coverage (FR1)
        if (c.DistanceKm > MaxDistanceKm)
            list.Add(new RuleViolation("BR2",
                $"Delivery distance is {c.DistanceKm.ToString("0.##", CultureInfo.InvariantCulture)} km; coverage is {MaxDistanceKm.ToString("0.0", CultureInfo.InvariantCulture)} km or less.",
                "Choose a nearer address or another hub."));

        // BR8 hub protection (FR4)
        if (c.IsNewOrder && c.HubLoadPct >= BusyFromPct)
            list.Add(new RuleViolation("BR8",
                $"This hub is at {c.HubLoadPct.ToString("0.##", CultureInfo.InvariantCulture)}% load and is not accepting new orders.",
                "Choose another hub."));

        return list;
    }

    /// <summary>Merges lines of the same item into one (OrderLines has primary key OrderId + ItemId).</summary>
    public static List<LineRequest> Merge(IEnumerable<LineRequest> lines) =>
        lines.GroupBy(l => l.ItemId)
             .Select(g => new LineRequest(g.Key, g.Sum(x => x.Quantity)))
             .ToList();

    public static string Vnd(decimal amount) =>
        amount.ToString("N0", CultureInfo.InvariantCulture) + " VND";
}
