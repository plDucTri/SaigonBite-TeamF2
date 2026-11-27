using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaigonBite.Helpers;
using SaigonBite.Services;
using SaigonBite.ViewModels;

namespace SaigonBite.Controllers;

/// <summary>UC1, Shipper side (Figma D11 to D13): accept a job, pick up, confirm cash, deliver or report a failed delivery.</summary>
[Authorize(Roles = Roles.Shipper)]
public class ShipperOrdersController : Controller
{
    private readonly IOrderService _orders;

    public ShipperOrdersController(IOrderService orders)
    {
        _orders = orders;
    }

    private int UserId => User.GetUserId();

    [HttpGet]
    public async Task<IActionResult> Jobs()
    {
        await _orders.AssignOverdueJobsAsync();   // A-01: also checked here so the list is up to date when the page opens

        var ready = await _orders.GetReadyJobsAsync();
        var mine = await _orders.GetShipperOrdersAsync(UserId);

        ViewBag.Mine = mine.Where(o => !OrderStatus.IsTerminal(o.Status)).Select(OrderViewModelFactory.Row).ToList();
        // Jobs nobody has accepted yet
        return View(ready.Where(o => o.ShipperId == null).Select(OrderViewModelFactory.Row).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var order = await _orders.GetOrderAsync(id, UserId);
        if (order == null) return NotFound();
        return View(OrderViewModelFactory.Details(order, Roles.Shipper));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id) =>
        Done(await _orders.AcceptJobAsync(id, UserId), id);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PickUp(int id) =>
        Done(await _orders.ChangeStatusAsync(id, UserId, OrderStatus.OutForDelivery), id);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmCash(int id) =>
        Done(await _orders.ConfirmCashAsync(id, UserId), id);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deliver(int id) =>
        Done(await _orders.ChangeStatusAsync(id, UserId, OrderStatus.Delivered), id);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Fail(int id, string? reason) =>
        Done(await _orders.ChangeStatusAsync(id, UserId, OrderStatus.Failed,
                string.IsNullOrWhiteSpace(reason) ? "Customer unreachable" : reason), id);

    private IActionResult Done(OperationResult result, int id)
    {
        if (!result.Success) TempData["Error"] = string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Details), new { id });
    }
}
