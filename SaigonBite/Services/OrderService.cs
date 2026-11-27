using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Repositories;
using SaigonBite.Services.Payments;

namespace SaigonBite.Services;

/// <summary>
/// CRC card OrderService (report 2.4): coordinates place, edit, cancel, change status and track. The rules live in
/// the helpers it calls: OrderValidator (BR1, BR2, BR8, BR12), PricingEngine (BR3 to BR11), PaymentService with the
/// payment strategies (BR13, BR14) and OrderStateMachine (BR15). OrderService itself checks the conditions that
/// need data (hub of the order, reason, assigned shipper, cash confirmed) and saves the result.
/// </summary>
public class OrderService : IOrderService
{
    /// <summary>A-01: how long a Ready order waits for a shipper to accept before the system assigns one.</summary>
    public static readonly TimeSpan AutoAssignAfter = TimeSpan.FromMinutes(5);

    private readonly IOrderRepository _orders;
    private readonly IOrderCatalogRepository _catalog;
    private readonly IVoucherRepository _vouchers;
    private readonly IUserRepository _users;
    private readonly IOrderValidator _validator;
    private readonly IPricingEngine _pricing;
    private readonly IPaymentService _payments;
    private readonly IHeavyRainFlag _rain;
    private readonly TimeProvider _time;

    public OrderService(
        IOrderRepository orders,
        IOrderCatalogRepository catalog,
        IVoucherRepository vouchers,
        IUserRepository users,
        IOrderValidator validator,
        IPricingEngine pricing,
        IPaymentService payments,
        IHeavyRainFlag rain,
        TimeProvider time)
    {
        _orders = orders;
        _catalog = catalog;
        _vouchers = vouchers;
        _users = users;
        _validator = validator;
        _pricing = pricing;
        _payments = payments;
        _rain = rain;
        _time = time;
    }

    /// <summary>Vietnam local time (UTC+7), the time of BR4 and of every timestamp we store.</summary>
    private DateTime Now => _time.GetUtcNow().UtcDateTime.AddHours(7);

    // ------------------------------------------------------------------ catalogue reads

    public async Task<List<HubLoad>> GetHubLoadsAsync()
    {
        var result = new List<HubLoad>();
        foreach (var hub in await _catalog.GetActiveHubsAsync())
            result.Add(new HubLoad(hub, await LoadPctAsync(hub)));
        return result;
    }

    public async Task<decimal> GetHubLoadPctAsync(int hubId)
    {
        var hub = await _catalog.GetHubWithBrandsAsync(hubId);
        return hub == null ? 0m : await LoadPctAsync(hub);
    }

    public Task<Hub?> GetHubAsync(int hubId) => _catalog.GetHubWithBrandsAsync(hubId);

    public Task<List<MenuItem>> GetMenuAsync(int hubId) => _catalog.GetHubMenuAsync(hubId);

    public Task<List<MenuItem>> GetItemsAsync(IEnumerable<int> itemIds) => _catalog.GetMenuItemsAsync(itemIds);

    /// <summary>BR8 (A-03 of the load): (Confirmed + Preparing) / KitchenCapacity in percent, rounded to 2 decimals.</summary>
    private async Task<decimal> LoadPctAsync(Hub hub)
    {
        int active = await _orders.CountActiveLoadAsync(hub.HubId);
        return Math.Round(active * 100m / hub.KitchenCapacity, 2, MidpointRounding.AwayFromZero);
    }

    // ------------------------------------------------------------------ place an order

    public async Task<CheckoutPreview?> PreviewAsync(int customerId, CheckoutRequest request)
    {
        var customer = GetCustomer(customerId);
        var hub = await _catalog.GetHubWithBrandsAsync(request.HubId);
        if (customer == null || hub == null || !hub.IsActive) return null;

        var ctx = await BuildPlacementAsync(hub, request);
        decimal load = await LoadPctAsync(hub);

        var pricing = _pricing.Calculate(new PricingRequest
        {
            Subtotal = ctx.Subtotal,
            DistanceKm = request.DistanceKm,
            HubLoadPct = load,
            PlacedAt = Now,
            HeavyRain = _rain.IsOn,
            Voucher = ctx.Voucher
        });

        var preview = new CheckoutPreview
        {
            CustomerRole = customer.Role,
            HubName = hub.Name,
            HubLoadPct = load,
            HeavyRain = _rain.IsOn,
            Pricing = pricing,
            Violations = _validator.Validate(ctx.ToCheck(request.DistanceKm, load, isNewOrder: true)),
            PaymentOptions = _payments.GetOptions(customer.Role, pricing.Total)
        };
        preview.Warnings.AddRange(ctx.Warnings);
        if (pricing.VoucherRefusedReason != null) preview.Warnings.Add(pricing.VoucherRefusedReason);
        return preview;
    }

    public async Task<OrderOutcome> PlaceOrderAsync(int customerId, CheckoutRequest request)
    {
        var outcome = new OrderOutcome { HubId = request.HubId };

        var customer = GetCustomer(customerId);
        if (customer == null)
        {
            outcome.Violations.Add(new RuleViolation("AUTH", "Only an active customer account can place orders.", "Log in again."));
            return outcome;
        }

        var hub = await _catalog.GetHubWithBrandsAsync(request.HubId);
        if (hub == null || !hub.IsActive)
        {
            outcome.Violations.Add(new RuleViolation("BR12", "The chosen hub does not exist or is closed.", "Choose another hub."));
            return outcome;
        }

        // Facts at the moment of placement: these become the price snapshot (BR17, FR11)
        var now = Now;
        decimal load = await LoadPctAsync(hub);
        bool rain = _rain.IsOn;
        outcome.HubLoadPct = load;

        // BR1, BR2, BR8, BR12 (OrderValidator)
        var ctx = await BuildPlacementAsync(hub, request);
        outcome.Violations.AddRange(_validator.Validate(ctx.ToCheck(request.DistanceKm, load, isNewOrder: true)));
        if (outcome.Violations.Count > 0) return outcome;   // nothing is saved

        // BR3 to BR11 (PricingEngine). A voucher that does not qualify is refused; the order goes on without it.
        var pricing = _pricing.Calculate(new PricingRequest
        {
            Subtotal = ctx.Subtotal,
            DistanceKm = request.DistanceKm,
            HubLoadPct = load,
            PlacedAt = now,
            HeavyRain = rain,
            Voucher = ctx.Voucher
        });
        outcome.Warnings.AddRange(ctx.Warnings);
        if (pricing.VoucherRefusedReason != null) outcome.Warnings.Add(pricing.VoucherRefusedReason);

        // BR13, BR14 and the simulated payment, taken BEFORE the order exists (A-03: a failed payment creates no order)
        var payment = _payments.Process(customer.Role, request.PaymentMethod, pricing.Total);
        if (payment.Violation != null)
        {
            outcome.Violations.Add(payment.Violation);
            return outcome;
        }
        if (!payment.Success)
        {
            outcome.PaymentFailed = true;
            outcome.Message = payment.Message;
            return outcome;
        }

        // The order, its lines, snapshot, payment and first history row are saved in ONE SaveChanges (NFR5)
        var order = new Order
        {
            CustomerId = customer.UserId,
            HubId = hub.HubId,
            VoucherCode = pricing.VoucherApplied ? ctx.Voucher!.Code : null,
            Status = OrderStatus.Placed,
            DistanceKm = request.DistanceKm,
            DeliveryAddress = request.DeliveryAddress.Trim(),
            SubtotalS = pricing.Subtotal,
            QuietDiscountQ = pricing.QuietDiscount,
            VoucherDiscountV = pricing.VoucherDiscount,
            DeliveryFeeD = pricing.DeliveryFee,
            RushSurchargeR = pricing.RushSurcharge,
            RainSurchargeR2 = pricing.RainSurcharge,
            Total = pricing.Total,
            CreatedAt = now
        };

        foreach (var line in ctx.Lines)
            order.OrderLines.Add(new OrderLine { ItemId = line.Item.ItemId, Quantity = line.Quantity, UnitPrice = line.UnitPrice });

        order.PriceSnapshot = new PriceSnapshot { PlacedAt = now, HubLoadPct = load, HeavyRain = rain };
        order.Payment = new Payment { Method = payment.Method, Amount = pricing.Total, Status = payment.Status, Refunded = false };
        order.OrderStatusHistories.Add(new OrderStatusHistory
        {
            FromStatus = null,
            ToStatus = OrderStatus.Placed,
            ChangedAt = now,
            ChangedBy = customer.UserId
        });

        await _orders.AddAsync(order);
        await _orders.SaveAsync();

        outcome.Success = true;
        outcome.OrderId = order.OrderId;
        return outcome;
    }

    private sealed class Placement
    {
        public List<LineRequest> Merged { get; set; } = new();
        public Dictionary<int, MenuItem> Items { get; set; } = new();
        public HashSet<int> BrandIds { get; set; } = new();
        public List<EditLine> Lines { get; } = new();
        public List<string> Warnings { get; } = new();
        public Voucher? Voucher { get; set; }
        public decimal Subtotal { get; set; }

        public OrderCheck ToCheck(decimal km, decimal load, bool isNewOrder) => new()
        {
            Subtotal = Subtotal,
            DistanceKm = km,
            HubLoadPct = load,
            Lines = Merged,
            Items = Items,
            HubBrandIds = BrandIds,
            IsNewOrder = isNewOrder
        };
    }

    /// <summary>Merges the lines, loads the items, adds up S (BR10 step 1) and finds the voucher (BR9).</summary>
    private async Task<Placement> BuildPlacementAsync(Hub hub, CheckoutRequest request)
    {
        var p = new Placement { Merged = OrderValidator.Merge(request.Lines) };
        p.Items = (await _catalog.GetMenuItemsAsync(p.Merged.Select(l => l.ItemId))).ToDictionary(i => i.ItemId);
        p.BrandIds = hub.Brands.Where(b => b.IsActive).Select(b => b.BrandId).ToHashSet();

        foreach (var line in p.Merged)
            if (p.Items.TryGetValue(line.ItemId, out var item))
            {
                p.Lines.Add(new EditLine(item, line.Quantity, item.Price));
                p.Subtotal += item.Price * line.Quantity;
            }

        // At most one voucher per order (BR9): the first code counts, any other is ignored with a note (T-17)
        var codes = (request.VoucherCode ?? "")
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (codes.Count > 0)
        {
            p.Voucher = await _vouchers.GetByCodeAsync(codes[0]);
            if (p.Voucher == null)
                p.Warnings.Add($"Voucher {codes[0]} does not exist.");
        }
        foreach (var extra in codes.Skip(1))
            p.Warnings.Add($"Only one voucher can be used per order (BR9): {extra} was not applied.");

        return p;
    }

    private User? GetCustomer(int userId)
    {
        var user = _users.GetById(userId);
        if (user == null || !user.IsActive) return null;
        return (user.Role == Roles.Local || user.Role == Roles.Tourist) ? user : null;
    }

    // ------------------------------------------------------------------ edit while Placed (FR11, BR17)

    public async Task<EditEvaluation> EvaluateEditAsync(int orderId, int customerId, IEnumerable<LineRequest> lines)
    {
        var eval = new EditEvaluation();
        var order = await _orders.GetWithDetailsAsync(orderId);
        if (order == null || order.CustomerId != customerId)
        {
            eval.Violations.Add(new RuleViolation("AUTH", "Order not found.", ""));
            return eval;
        }
        eval.Order = order;

        if (order.Status != OrderStatus.Placed)
        {
            eval.Violations.Add(new RuleViolation("BR17",
                $"Items can be edited only while the order is Placed (this order is {order.Status}).", ""));
            return eval;
        }
        if (order.PriceSnapshot == null)
        {
            eval.Violations.Add(new RuleViolation("BR17", "This order has no price snapshot, so it cannot be recalculated.", ""));
            return eval;
        }

        var merged = OrderValidator.Merge(lines);
        var items = (await _catalog.GetMenuItemsAsync(merged.Select(l => l.ItemId))).ToDictionary(i => i.ItemId);

        // Items already in the order keep the price they had at placement; new items use the current price (A-09)
        var oldPrices = order.OrderLines.ToDictionary(l => l.ItemId, l => l.UnitPrice);
        decimal subtotal = 0m;
        foreach (var line in merged)
            if (items.TryGetValue(line.ItemId, out var item))
            {
                decimal unit = oldPrices.TryGetValue(line.ItemId, out var old) ? old : item.Price;
                eval.Lines.Add(new EditLine(item, line.Quantity, unit));
                subtotal += unit * line.Quantity;
            }

        var snap = order.PriceSnapshot;

        // BR1, BR2 and BR12 apply again; BR8 protects new orders only (A-10)
        eval.Violations.AddRange(_validator.Validate(new OrderCheck
        {
            Subtotal = subtotal,
            DistanceKm = order.DistanceKm,
            HubLoadPct = snap.HubLoadPct,
            Lines = merged,
            Items = items,
            HubBrandIds = order.Hub.Brands.Where(b => b.IsActive).Select(b => b.BrandId).ToHashSet(),
            IsNewOrder = false
        }));

        // Recalculate from the SNAPSHOT, never from the current hub load, time or rain flag (BR17, NFR6)
        eval.Pricing = _pricing.Calculate(new PricingRequest
        {
            Subtotal = subtotal,
            DistanceKm = order.DistanceKm,
            HubLoadPct = snap.HubLoadPct,
            PlacedAt = snap.PlacedAt,
            HeavyRain = snap.HeavyRain,
            Voucher = order.VoucherCodeNavigation
        });
        if (eval.Pricing.VoucherRefusedReason != null) eval.Warnings.Add(eval.Pricing.VoucherRefusedReason);

        // BR14: a COD order must stay within the limit after the edit
        if (order.Payment != null)
        {
            var options = _payments.GetOptions(order.Customer.Role, eval.Pricing.Total);
            var chosen = options.FirstOrDefault(o => string.Equals(o.Method, order.Payment.Method, StringComparison.OrdinalIgnoreCase));
            if (chosen != null && !chosen.Enabled)
                eval.Violations.Add(new RuleViolation("BR14",
                    $"{order.Payment.Method} cannot be used for the new total of {eval.Pricing.Total:N0} VND.",
                    "Reduce the order."));
        }
        return eval;
    }

    public async Task<OperationResult> EditOrderAsync(int orderId, int customerId, IEnumerable<LineRequest> lines)
    {
        var eval = await EvaluateEditAsync(orderId, customerId, lines);
        if (!eval.Success) return OperationResult.Fail(eval.Violations);   // the order stays unchanged

        var order = eval.Order!;
        var p = eval.Pricing;

        // Update the lines in place (the key is OrderId + ItemId, so we avoid deleting and adding the same key)
        var keep = eval.Lines.ToDictionary(l => l.Item.ItemId);
        foreach (var old in order.OrderLines.Where(l => !keep.ContainsKey(l.ItemId)).ToList())
            _orders.RemoveLine(old);

        foreach (var l in eval.Lines)
        {
            var existing = order.OrderLines.FirstOrDefault(x => x.ItemId == l.Item.ItemId);
            if (existing != null) existing.Quantity = l.Quantity;
            else order.OrderLines.Add(new OrderLine { OrderId = order.OrderId, ItemId = l.Item.ItemId, Quantity = l.Quantity, UnitPrice = l.UnitPrice });
        }

        order.SubtotalS = p.Subtotal;
        order.QuietDiscountQ = p.QuietDiscount;
        order.VoucherDiscountV = p.VoucherDiscount;
        order.DeliveryFeeD = p.DeliveryFee;
        order.RushSurchargeR = p.RushSurcharge;
        order.RainSurchargeR2 = p.RainSurcharge;
        order.Total = p.Total;
        if (order.Payment != null) order.Payment.Amount = p.Total;

        await _orders.SaveAsync();

        var result = OperationResult.Ok();
        result.Warnings.AddRange(eval.Warnings);
        return result;
    }

    // ------------------------------------------------------------------ lifecycle (FR8, FR10, BR15, BR16)

    public Task<OperationResult> CancelAsync(int orderId, int actorId, string? reason = null) =>
        ChangeStatusAsync(orderId, actorId, OrderStatus.Cancelled, reason);

    public async Task<OperationResult> ChangeStatusAsync(int orderId, int actorId, string toStatus, string? reason = null)
    {
        var actor = _users.GetById(actorId);
        if (actor == null || !actor.IsActive)
            return OperationResult.Fail("AUTH: unknown or inactive user.");

        var order = await _orders.GetWithDetailsAsync(orderId);
        if (order == null)
            return OperationResult.Fail("Order not found.");

        // 1. OrderStateMachine: is this pair allowed for this role? (BR15)
        var check = OrderStateMachine.Check(order.Status, toStatus, actor.Role);
        if (!check.Allowed) return OperationResult.Fail(check.Error!);

        // 2. Conditions of section C that need data
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if ((actor.Role == Roles.Local || actor.Role == Roles.Tourist) && order.CustomerId != actor.UserId)
            return OperationResult.Fail("BR15: this is not your order.");

        if (actor.Role == Roles.Kitchen && actor.HubId != order.HubId)
            return OperationResult.Fail("BR15: kitchen staff can only handle orders of their own hub.");

        if (order.Status == OrderStatus.Placed && toStatus == OrderStatus.Cancelled &&
            actor.Role == Roles.Kitchen && reason == null)
            return OperationResult.Fail("BR16: kitchen staff must give a reason when rejecting an order.");

        if (order.Status == OrderStatus.Ready && toStatus == OrderStatus.OutForDelivery)
        {
            if (order.ShipperId == null)
                return OperationResult.Fail("BR15: a shipper must be assigned before the order goes out for delivery.");
            if (order.ShipperId != actor.UserId)
                return OperationResult.Fail("BR15: this order is assigned to another shipper.");
        }

        if (order.Status == OrderStatus.OutForDelivery && order.ShipperId != actor.UserId)
            return OperationResult.Fail("BR15: only the assigned shipper can finish this delivery.");

        if (toStatus == OrderStatus.Delivered &&
            order.Payment != null && order.Payment.Method == "COD" &&
            order.Payment.Status != PaymentStatus.CashCollected)
            return OperationResult.Fail("BR14: confirm the cash was collected before marking a COD order Delivered.");

        // 3. Apply: status, history row with time and actor (NFR5), refund (BR16, A-02)
        string from = order.Status;
        order.Status = toStatus;
        order.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.OrderId,
            FromStatus = from,
            ToStatus = toStatus,
            ChangedAt = Now,
            ChangedBy = actor.UserId,
            Reason = reason
        });

        if ((toStatus == OrderStatus.Cancelled || toStatus == OrderStatus.Failed) && order.Payment != null)
            _payments.Refund(order.Payment);

        await _orders.SaveAsync();
        return OperationResult.Ok();
    }

    /// <summary>A-01: the first shipper to accept a Ready order is assigned. The status stays Ready until pick-up is confirmed.</summary>
    public async Task<OperationResult> AcceptJobAsync(int orderId, int shipperId)
    {
        var shipper = _users.GetById(shipperId);
        if (shipper == null || !shipper.IsActive || shipper.Role != Roles.Shipper)
            return OperationResult.Fail("AUTH: only an active shipper can accept a delivery job.");

        var order = await _orders.GetWithDetailsAsync(orderId);
        if (order == null) return OperationResult.Fail("Order not found.");
        if (order.Status != OrderStatus.Ready)
            return OperationResult.Fail($"BR15: only a Ready order can be accepted (this order is {order.Status}).");
        if (order.ShipperId != null && order.ShipperId != shipperId)
            return OperationResult.Fail("BR15: another shipper already accepted this order.");

        order.ShipperId = shipperId;
        await _orders.SaveAsync();
        return OperationResult.Ok();
    }

    /// <summary>BR14: the shipper confirms the cash of a COD order before it can become Delivered.</summary>
    public async Task<OperationResult> ConfirmCashAsync(int orderId, int shipperId)
    {
        var shipper = _users.GetById(shipperId);
        if (shipper == null || !shipper.IsActive || shipper.Role != Roles.Shipper)
            return OperationResult.Fail("AUTH: only an active shipper can confirm cash.");

        var order = await _orders.GetWithDetailsAsync(orderId);
        if (order == null) return OperationResult.Fail("Order not found.");
        if (order.Status != OrderStatus.OutForDelivery || order.ShipperId != shipperId)
            return OperationResult.Fail("BR14: cash can be confirmed only by the assigned shipper while the order is Out for Delivery.");
        if (order.Payment == null || order.Payment.Method != "COD")
            return OperationResult.Fail("BR14: this order was not paid by Cash on Delivery.");
        if (order.Payment.Status == PaymentStatus.CashCollected)
            return OperationResult.Fail("BR14: the cash was already confirmed.");

        order.Payment.Status = PaymentStatus.CashCollected;
        await _orders.SaveAsync();
        return OperationResult.Ok();
    }

    private sealed class ShipperLoad
    {
        public int ShipperId { get; init; }
        public int Active { get; set; }
        public DateTime LastActivity { get; set; }
    }

    /// <summary>
    /// A-01: a Ready order that nobody accepted for 5 minutes goes to the available shipper with the fewest active
    /// deliveries; on a tie, to the shipper who has waited longest without an order (T-54).
    /// </summary>
    public async Task<int> AssignOverdueJobsAsync()
    {
        var now = Now;
        var overdue = await _orders.GetOverdueReadyAsync(now - AutoAssignAfter);
        if (overdue.Count == 0) return 0;

        var shippers = await _catalog.GetActiveShippersAsync();
        if (shippers.Count == 0) return 0;

        var activity = await _orders.GetShipperActivityAsync();
        var load = shippers.Select(s =>
        {
            activity.TryGetValue(s.UserId, out var a);
            return new ShipperLoad
            {
                ShipperId = s.UserId,
                Active = a?.ActiveDeliveries ?? 0,
                LastActivity = a?.LastActivity ?? DateTime.MinValue     // never had an order: has waited longest
            };
        }).ToList();

        foreach (var order in overdue)
        {
            var pick = load.OrderBy(l => l.Active).ThenBy(l => l.LastActivity).ThenBy(l => l.ShipperId).First();
            order.ShipperId = pick.ShipperId;
            pick.Active++;
            pick.LastActivity = now;
        }

        await _orders.SaveAsync();
        return overdue.Count;
    }

    // ------------------------------------------------------------------ track (FR9, NFR4, NFR5)

    /// <summary>Returns the order only if this user may see it (own order, own hub, assigned or Ready job, or Admin).</summary>
    public async Task<Order?> GetOrderAsync(int orderId, int viewerId)
    {
        var viewer = _users.GetById(viewerId);
        if (viewer == null || !viewer.IsActive) return null;

        var order = await _orders.GetWithDetailsAsync(orderId);
        if (order == null) return null;

        bool allowed = viewer.Role switch
        {
            Roles.Admin => true,
            Roles.Local or Roles.Tourist => order.CustomerId == viewer.UserId,
            Roles.Kitchen => viewer.HubId == order.HubId,
            Roles.Shipper => order.ShipperId == viewer.UserId || order.Status == OrderStatus.Ready,
            _ => false
        };
        return allowed ? order : null;
    }

    public Task<List<Order>> TrackAsync(int customerId, string? status, DateTime? from, DateTime? to) =>
        _orders.GetByCustomerAsync(customerId, status, from, to);

    public Task<List<Order>> GetKitchenQueueAsync(int kitchenUserId)
    {
        var user = _users.GetById(kitchenUserId);
        if (user == null || user.Role != Roles.Kitchen || user.HubId == null)
            return Task.FromResult(new List<Order>());
        return _orders.GetByHubAsync(user.HubId.Value);
    }

    public Task<List<Order>> GetReadyJobsAsync() => _orders.GetByStatusAsync(OrderStatus.Ready);

    public Task<List<Order>> GetShipperOrdersAsync(int shipperId) => _orders.GetByShipperAsync(shipperId);

    public Task<List<Order>> GetAllOrdersAsync(string? status, int? hubId) => _orders.GetAllAsync(status, hubId);
}
