using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaigonBite.Helpers;

namespace SaigonBite.Controllers;

/// <summary>
/// TEMPORARY pages used to prove the role checks (test T-59).
/// Delete this controller and its view when the real Admin, Kitchen, Shipper and Customer pages exist.
/// </summary>
public class AccessCheckController : Controller
{
    [Authorize(Roles = Roles.Admin)]
    public IActionResult Admin() => Show("Admin");

    [Authorize(Roles = Roles.Kitchen)]
    public IActionResult Kitchen() => Show("Kitchen staff");

    [Authorize(Roles = Roles.Shipper)]
    public IActionResult Shipper() => Show("Shipper");

    [Authorize(Roles = Roles.Customers)]
    public IActionResult Customer() => Show("Customer");

    private IActionResult Show(string who)
    {
        ViewData["Who"] = who;
        return View("Page");
    }
}
