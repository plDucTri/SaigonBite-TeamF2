using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Services;

namespace SaigonBite.ViewModels;

/// <summary>Maps order entities to view models. No business rules: it only copies and formats data.</summary>
public static class OrderViewModelFactory
{
    public static OrderRowViewModel Row(Order o) => new()
    {
        OrderId = o.OrderId,
        PlacedAt = o.CreatedAt,
        HubName = o.Hub?.Name ?? "",
        CustomerName = o.Customer?.FullName ?? "",
        Total = o.Total,
        Status = o.Status,
        PaymentMethod = o.Payment?.Method ?? "",
        PaymentStatus = o.Payment?.Status ?? "",
        ShipperId = o.ShipperId
    };

    public static OrderDetailsViewModel Details(Order o, string viewerRole)
    {
        var vm = new OrderDetailsViewModel
        {
            OrderId = o.OrderId,
            Status = o.Status,
            PlacedAt = o.CreatedAt,
            HubName = o.Hub?.Name ?? "",
            CustomerName = o.Customer?.FullName ?? "",
            Address = o.DeliveryAddress,
            DistanceKm = o.DistanceKm,
            ShipperId = o.ShipperId,
            Subtotal = o.SubtotalS,
            Quiet = o.QuietDiscountQ,
            Voucher = o.VoucherDiscountV,
            VoucherCode = o.VoucherCode,
            Delivery = o.DeliveryFeeD,
            Rush = o.RushSurchargeR,
            Rain = o.RainSurchargeR2,
            Total = o.Total,
            PaymentMethod = o.Payment?.Method ?? "",
            PaymentStatus = o.Payment?.Status ?? "",
            Refunded = o.Payment?.Refunded ?? false,
            SnapshotAt = o.PriceSnapshot?.PlacedAt,
            SnapshotLoadPct = o.PriceSnapshot?.HubLoadPct,
            SnapshotRain = o.PriceSnapshot?.HeavyRain,
            NextStatuses = OrderStateMachine.NextStatuses(o.Status, viewerRole).ToList()
        };

        vm.CanEdit = o.Status == OrderStatus.Placed &&
                     (viewerRole == Roles.Local || viewerRole == Roles.Tourist);

        vm.Lines = o.OrderLines
            .OrderBy(l => l.ItemId)
            .Select(l => new OrderLineRowViewModel
            {
                ItemId = l.ItemId,
                Name = l.Item?.Name ?? ("Item #" + l.ItemId),
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice
            }).ToList();

        vm.History = o.OrderStatusHistories
            .OrderBy(h => h.ChangedAt).ThenBy(h => h.HistoryId)
            .Select(h => new StatusHistoryRowViewModel
            {
                From = h.FromStatus,
                To = h.ToStatus,
                At = h.ChangedAt,
                By = h.ChangedByNavigation?.FullName ?? ("User #" + h.ChangedBy),
                Reason = h.Reason
            }).ToList();

        return vm;
    }
}
