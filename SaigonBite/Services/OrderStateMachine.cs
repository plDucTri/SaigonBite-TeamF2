using SaigonBite.Helpers;

namespace SaigonBite.Services;

public record TransitionCheck(bool Allowed, string? Error);

/// <summary>
/// CRC card OrderStateMachine: the lifecycle table of section C / BR15. Any pair not listed is invalid, and the
/// check runs in the business layer (OrderService), not only in the UI. Conditions that need data (hub of the
/// order, reason, assigned shipper, cash confirmed) are checked by OrderService after this table allows the pair.
/// </summary>
public static class OrderStateMachine
{
    private sealed record Rule(string From, string To, string[] Roles);

    private static readonly Rule[] Rules =
    {
        new(OrderStatus.Placed,         OrderStatus.Confirmed,      new[] { Roles.Kitchen }),
        new(OrderStatus.Placed,         OrderStatus.Cancelled,      new[] { Roles.Local, Roles.Tourist, Roles.Kitchen }),
        new(OrderStatus.Confirmed,      OrderStatus.Preparing,      new[] { Roles.Kitchen }),
        new(OrderStatus.Confirmed,      OrderStatus.Cancelled,      new[] { Roles.Local, Roles.Tourist, Roles.Admin }),
        new(OrderStatus.Preparing,      OrderStatus.Ready,          new[] { Roles.Kitchen }),
        new(OrderStatus.Ready,          OrderStatus.OutForDelivery, new[] { Roles.Shipper }),
        new(OrderStatus.OutForDelivery, OrderStatus.Delivered,      new[] { Roles.Shipper }),
        new(OrderStatus.OutForDelivery, OrderStatus.Failed,         new[] { Roles.Shipper }),
    };

    public static TransitionCheck Check(string from, string to, string actorRole)
    {
        var pair = Rules.Where(r => r.From == from && r.To == to).ToList();
        if (pair.Count == 0)
            return new TransitionCheck(false, $"BR15: an order cannot go from {from} to {to}.");

        if (!pair.Any(r => r.Roles.Contains(actorRole)))
            return new TransitionCheck(false, $"BR15: a {actorRole} user may not change an order from {from} to {to}.");

        return new TransitionCheck(true, null);
    }

    /// <summary>Statuses this role may move an order to from the given status (used to show buttons).</summary>
    public static IReadOnlyList<string> NextStatuses(string from, string actorRole) =>
        Rules.Where(r => r.From == from && r.Roles.Contains(actorRole)).Select(r => r.To).ToList();
}
