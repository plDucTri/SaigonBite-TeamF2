using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaigonBite.Helpers;
using SaigonBite.Services;
using SaigonBite.ViewModels;

namespace SaigonBite.Controllers;

/// <summary>UC1, Kitchen Staff side (Figma D09, D10): confirm, prepare, mark ready, reject with a reason.</summary>
[Authorize(Roles = Roles.Kitchen)]
public class KitchenOrdersController : Controller
{
    private readonly IOrderService _orders;

    public KitchenOrdersController(IOrderService orders)
    {
        _orders = orders;
    }

    private int UserId => User.GetUserId();

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var list = await _orders.GetKitchenQueueAsync(UserId);
        return View(list.Select(OrderViewModelFactory.Row).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var order = await _orders.GetOrderAsync(id, UserId);
        if (order == null) return NotFound();
        return View(OrderViewModelFactory.Details(order, Roles.Kitchen));
    }

    /// <summary>Moves an order to the next state. OrderService decides whether the move is allowed (BR15).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Advance(int id, string to, string? reason)
    {
        var result = to == OrderStatus.Cancelled
            ? await _orders.CancelAsync(id, UserId, reason)
            : await _orders.ChangeStatusAsync(id, UserId, to, reason);
        if (!result.Success) TempData["Error"] = string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index));
    }
}
