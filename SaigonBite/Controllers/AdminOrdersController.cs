using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaigonBite.Helpers;
using SaigonBite.Services;
using SaigonBite.Services.Payments;
using SaigonBite.ViewModels;

namespace SaigonBite.Controllers;

/// <summary>UC1, Admin side (Figma D15): see all orders, cancel a Confirmed order, set the heavy-rain flag (BR5).</summary>
[Authorize(Roles = Roles.Admin)]
public class AdminOrdersController : Controller
{
    private readonly IOrderService _orders;
    private readonly IHeavyRainFlag _rain;
    private readonly IGatewaySimulator _gateway;

    public AdminOrdersController(IOrderService orders, IHeavyRainFlag rain, IGatewaySimulator gateway)
    {
        _orders = orders;
        _rain = rain;
        _gateway = gateway;
    }

    private int UserId => User.GetUserId();

    [HttpGet]
    public async Task<IActionResult> Index(string? status, int? hubId)
    {
        var list = await _orders.GetAllOrdersAsync(status, hubId);
        ViewBag.Status = status;
        ViewBag.HubId = hubId;
        ViewBag.Hubs = (await _orders.GetHubLoadsAsync()).Select(h => h.Hub).ToList();
        return View(list.Select(OrderViewModelFactory.Row).ToList());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _orders.CancelAsync(id, UserId, "Cancelled by admin");
        TempData[result.Success ? "Info" : "Error"] = result.Success
            ? OrderLabel.For(id) + " cancelled."
            : string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index));
    }

    // ---- Heavy Rain Flag (BR5) and the mock payment gateway (spec section G, A-03) ----

    [HttpGet]
    public IActionResult Simulation() =>
        View(new SimulationViewModel { HeavyRain = _rain.IsOn, GatewayDeclines = _gateway.DeclineAll });

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Simulation(bool heavyRain, bool gatewayDeclines)
    {
        _rain.Set(heavyRain);
        _gateway.SetDeclineAll(gatewayDeclines);
        TempData["Info"] = "Settings saved.";
        return RedirectToAction(nameof(Simulation));
    }
}
