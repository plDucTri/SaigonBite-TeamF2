using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaigonBite.Helpers;
using SaigonBite.Services;
using SaigonBite.ViewModels;

namespace SaigonBite.Controllers;

/// <summary>
/// UC1 customer screens: browse menu, cart, checkout, my orders, details, edit, cancel (Figma D01 to D08, D12, D14, D16 to D18).
/// The controller only reads the request, calls IOrderService and picks a view. No pricing or lifecycle rule is here (NFR2).
/// </summary>
[Authorize(Roles = Roles.Customers)]
public class OrdersController : Controller
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders)
    {
        _orders = orders;
    }

    private int UserId => User.GetUserId();
    private string UserRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "";

    // ---------------------------------------------------------------- D01 browse menu

    [HttpGet]
    public async Task<IActionResult> Menu(int? hubId)
    {
        var cart = CartSession.Get(HttpContext.Session);
        var hubs = await _orders.GetHubLoadsAsync();
        if (hubs.Count == 0) return View(new MenuViewModel());

        int selected = hubId ?? (cart.HubId > 0 ? cart.HubId : hubs[0].Hub.HubId);

        // Changing hub empties the cart: one order is built from the menus of ONE hub (FR1)
        if (cart.HubId != 0 && cart.HubId != selected && cart.Lines.Count > 0)
        {
            cart = new CartState();
            CartSession.Save(HttpContext.Session, cart);
            TempData["Info"] = "Your cart was cleared because you changed hub.";
        }

        var vm = new MenuViewModel
        {
            Hubs = hubs.Select(h => new HubOptionViewModel { HubId = h.Hub.HubId, Name = h.Hub.Name, LoadPct = h.LoadPct }).ToList(),
            CartCount = cart.Count
        };
        vm.SelectedHub = vm.Hubs.FirstOrDefault(h => h.HubId == selected) ?? vm.Hubs[0];

        var items = await _orders.GetMenuAsync(vm.SelectedHub.HubId);
        vm.Items = items.Select(i => new MenuItemRowViewModel
        {
            ItemId = i.ItemId, Name = i.Name, Brand = i.Brand.Name, Category = i.Category, Price = i.Price
        }).ToList();
        return View(vm);
    }

    // ---------------------------------------------------------------- D02 cart

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult AddToCart(int hubId, int itemId, int quantity = 1)
    {
        var cart = CartSession.Get(HttpContext.Session);
        if (cart.HubId != hubId)
            cart = new CartState { HubId = hubId };

        var existing = cart.Lines.FirstOrDefault(l => l.ItemId == itemId);
        int newQty = Math.Min((existing?.Quantity ?? 0) + Math.Max(1, quantity), OrderValidator.MaxQuantity);
        cart.Lines.RemoveAll(l => l.ItemId == itemId);
        cart.Lines.Add(new LineRequest(itemId, newQty));

        CartSession.Save(HttpContext.Session, cart);
        return RedirectToAction(nameof(Menu), new { hubId });
    }

    [HttpGet]
    public async Task<IActionResult> Cart()
    {
        var cart = CartSession.Get(HttpContext.Session);
        var vm = new CartViewModel { HubId = cart.HubId };
        if (cart.HubId == 0 || cart.Lines.Count == 0) return View(vm);

        var hub = await _orders.GetHubAsync(cart.HubId);
        vm.HubName = hub?.Name ?? "";
        vm.HubLoadPct = await _orders.GetHubLoadPctAsync(cart.HubId);

        var items = (await _orders.GetItemsAsync(cart.Lines.Select(l => l.ItemId))).ToDictionary(i => i.ItemId);
        foreach (var line in cart.Lines)
            if (items.TryGetValue(line.ItemId, out var item))
                vm.Lines.Add(new CartLineViewModel { ItemId = item.ItemId, Name = item.Name, UnitPrice = item.Price, Quantity = line.Quantity });
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult UpdateCart(Dictionary<int, int> qty)
    {
        var cart = CartSession.Get(HttpContext.Session);
        var lines = new List<LineRequest>();
        foreach (var line in cart.Lines)
        {
            int q = qty.TryGetValue(line.ItemId, out var posted) ? posted : line.Quantity;
            if (q >= 1) lines.Add(new LineRequest(line.ItemId, Math.Min(q, OrderValidator.MaxQuantity)));   // 0 removes the line
        }
        cart.Lines = lines;
        CartSession.Save(HttpContext.Session, cart);
        return RedirectToAction(nameof(Cart));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult RemoveFromCart(int[] selected)
    {
        var cart = CartSession.Get(HttpContext.Session);
        cart.Lines.RemoveAll(l => selected.Contains(l.ItemId));
        CartSession.Save(HttpContext.Session, cart);
        return RedirectToAction(nameof(Cart));
    }

    // ---------------------------------------------------------------- D03 / D04 / D18 checkout

    [HttpGet]
    public async Task<IActionResult> Checkout()
    {
        var cart = CartSession.Get(HttpContext.Session);
        if (cart.HubId == 0 || cart.Lines.Count == 0) return RedirectToAction(nameof(Cart));

        var vm = await BuildCheckoutAsync(cart, new CheckoutForm());
        return vm == null ? Forbid() : View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutForm form)
    {
        var cart = CartSession.Get(HttpContext.Session);
        if (cart.HubId == 0 || cart.Lines.Count == 0) return RedirectToAction(nameof(Cart));

        // "Recalculate" only shows the breakdown; nothing is saved
        if (form.Submit != "place")
        {
            var preview = await BuildCheckoutAsync(cart, form);
            return preview == null ? Forbid() : View(preview);
        }

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(form.DeliveryAddress)) missing.Add("Enter a delivery address.");
        if (!MoneyFormat.TryParseKm(form.Distance, out _)) missing.Add("Enter the distance from the hub in km.");
        if (string.IsNullOrWhiteSpace(form.PaymentMethod)) missing.Add("Choose a payment method.");
        if (missing.Count > 0)
        {
            var again = await BuildCheckoutAsync(cart, form);
            if (again == null) return Forbid();
            again.Messages.AddRange(missing);
            return View(again);
        }

        var outcome = await _orders.PlaceOrderAsync(UserId, ToRequest(cart, form));

        if (outcome.Success)
        {
            CartSession.Clear(HttpContext.Session);
            if (outcome.Warnings.Count > 0)
                TempData["Warning"] = string.Join(" ", outcome.Warnings);
            return RedirectToAction(nameof(Placed), new { id = outcome.OrderId });
        }

        if (outcome.HubTooBusy)                                   // D16
        {
            var hub = await _orders.GetHubAsync(cart.HubId);
            var all = await _orders.GetHubLoadsAsync();
            return View("HubBusy", new HubBusyViewModel
            {
                HubName = hub?.Name ?? "",
                HubLoadPct = outcome.HubLoadPct,
                OtherHubs = all.Where(h => h.Hub.HubId != cart.HubId)
                               .Select(h => new HubOptionViewModel { HubId = h.Hub.HubId, Name = h.Hub.Name, LoadPct = h.LoadPct })
                               .ToList()
            });
        }

        if (outcome.PaymentFailed)                                // D14
            return View("PaymentFailed", outcome.Message);

        return View("Rejected", new RejectedViewModel { Violations = outcome.Violations });   // D17
    }

    private static CheckoutRequest ToRequest(CartState cart, CheckoutForm form)
    {
        // A tourist can give a hotel name and a room (spec section A)
        string address = string.IsNullOrWhiteSpace(form.RoomNo)
            ? form.DeliveryAddress
            : form.DeliveryAddress.Trim() + ", room " + form.RoomNo.Trim();

        MoneyFormat.TryParseKm(form.Distance, out decimal km);

        return new CheckoutRequest
        {
            HubId = cart.HubId,
            Lines = cart.Lines.ToList(),
            DistanceKm = km,
            DeliveryAddress = address,
            VoucherCode = form.VoucherCode,
            PaymentMethod = form.PaymentMethod ?? ""
        };
    }

    private async Task<CheckoutViewModel?> BuildCheckoutAsync(CartState cart, CheckoutForm form)
    {
        var preview = await _orders.PreviewAsync(UserId, ToRequest(cart, form));
        if (preview == null) return null;

        return new CheckoutViewModel
        {
            Form = form,
            HubId = cart.HubId,
            HubName = preview.HubName,
            HubLoadPct = preview.HubLoadPct,
            IsTourist = preview.CustomerRole == Roles.Tourist,
            HeavyRain = preview.HeavyRain,
            Pricing = preview.Pricing,
            Violations = preview.Violations,
            Warnings = preview.Warnings,
            PaymentOptions = preview.PaymentOptions
        };
    }

    // ---------------------------------------------------------------- D05 placed, D07 details

    [HttpGet]
    public async Task<IActionResult> Placed(int id)
    {
        var order = await _orders.GetOrderAsync(id, UserId);
        if (order == null) return NotFound();
        return View(OrderViewModelFactory.Details(order, UserRole));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var order = await _orders.GetOrderAsync(id, UserId);
        if (order == null) return NotFound();
        return View(OrderViewModelFactory.Details(order, UserRole));
    }

    // ---------------------------------------------------------------- D06 my orders

    [HttpGet]
    public async Task<IActionResult> MyOrders(string? status, DateTime? from, DateTime? to)
    {
        var list = await _orders.TrackAsync(UserId, status, from, to);
        ViewBag.Status = status;
        ViewBag.From = from;
        ViewBag.To = to;
        return View(list.Select(OrderViewModelFactory.Row).ToList());
    }

    // ---------------------------------------------------------------- D08 edit items (Placed only)

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var order = await _orders.GetOrderAsync(id, UserId);
        if (order == null) return NotFound();

        var lines = order.OrderLines.Select(l => new LineRequest(l.ItemId, l.Quantity)).ToList();
        return View(await BuildEditAsync(id, lines, new List<string>()));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Dictionary<int, int> qty, int? addItemId, string submit = "recalc")
    {
        var lines = qty.Where(kv => kv.Value >= 1).Select(kv => new LineRequest(kv.Key, kv.Value)).ToList();
        if (addItemId.HasValue && !lines.Any(l => l.ItemId == addItemId.Value))
            lines.Add(new LineRequest(addItemId.Value, 1));

        if (submit == "save")
        {
            var result = await _orders.EditOrderAsync(id, UserId, lines);
            if (result.Success)
            {
                TempData["Info"] = "Order updated. The price was recalculated with the conditions from when you ordered.";
                if (result.Warnings.Count > 0) TempData["Warning"] = string.Join(" ", result.Warnings);
                return RedirectToAction(nameof(Details), new { id });
            }
            return View(await BuildEditAsync(id, lines, result.Errors));
        }

        return View(await BuildEditAsync(id, lines, new List<string>()));
    }

    private async Task<EditOrderViewModel> BuildEditAsync(int orderId, List<LineRequest> lines, List<string> errors)
    {
        var eval = await _orders.EvaluateEditAsync(orderId, UserId, lines);
        var vm = new EditOrderViewModel { OrderId = orderId, Pricing = eval.Pricing, Errors = errors, Warnings = eval.Warnings };

        if (eval.Order != null)
        {
            vm.SnapshotAt = eval.Order.PriceSnapshot?.PlacedAt ?? eval.Order.CreatedAt;
            vm.SnapshotLoadPct = eval.Order.PriceSnapshot?.HubLoadPct ?? 0m;
            vm.SnapshotRain = eval.Order.PriceSnapshot?.HeavyRain ?? false;

            vm.Lines = eval.Lines.Select(l => new OrderLineRowViewModel
            {
                ItemId = l.Item.ItemId, Name = l.Item.Name, Quantity = l.Quantity, UnitPrice = l.UnitPrice
            }).ToList();

            var inOrder = vm.Lines.Select(l => l.ItemId).ToHashSet();
            var menu = await _orders.GetMenuAsync(eval.Order.HubId);
            vm.AddableItems = menu.Where(m => !inOrder.Contains(m.ItemId))
                .Select(m => new MenuItemRowViewModel { ItemId = m.ItemId, Name = m.Name, Brand = m.Brand.Name, Category = m.Category, Price = m.Price })
                .ToList();

            if (errors.Count == 0)
                vm.Errors = eval.Violations.Select(v => v.Rule + ": " + v.Message).ToList();
        }
        else
        {
            vm.Errors = eval.Violations.Select(v => v.Message).ToList();
        }
        return vm;
    }

    // ---------------------------------------------------------------- D12 cancel and refund

    [HttpGet]
    public async Task<IActionResult> Cancel(int id)
    {
        var order = await _orders.GetOrderAsync(id, UserId);
        if (order == null) return NotFound();
        return View(OrderViewModelFactory.Details(order, UserRole));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelConfirmed(int id)
    {
        var result = await _orders.CancelAsync(id, UserId);
        TempData[result.Success ? "Info" : "Error"] = result.Success
            ? "Order cancelled."
            : string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Details), new { id });
    }
}
