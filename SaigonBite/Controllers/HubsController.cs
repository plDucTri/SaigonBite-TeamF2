using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaigonBite.Helpers;
using SaigonBite.Services;
using SaigonBite.ViewModels;

namespace SaigonBite.Controllers;

/// <summary>
/// Admin Portal: Hubs and Capacity (FR17, Figma A04 and A09). Only the Admin role may use it (NFR4).
/// It only passes data between the pages and IHubService: the hub rules are not in this class (NFR2).
/// </summary>
[Authorize(Roles = Roles.Admin)]
public class HubsController : Controller
{
    private readonly IHubService _hubs;

    public HubsController(IHubService hubs)
    {
        _hubs = hubs;
    }

    [HttpGet("Admin/Hubs")]
    public IActionResult Index()
    {
        return View(_hubs.ListHubs());
    }

    [HttpGet("Admin/Hubs/New")]
    public IActionResult NewHub()
    {
        var vm = new HubFormViewModel();
        vm.BrandOptions = _hubs.ListBrandOptions(null);
        return View("HubForm", vm);
    }

    [HttpPost("Admin/Hubs/New"), ValidateAntiForgeryToken]
    public IActionResult CreateHub(HubFormViewModel vm)
    {
        var result = _hubs.CreateHub(ToChanges(vm));
        if (result.Ok)
        {
            TempData["Notice"] = "Hub saved.";
            return RedirectToAction(nameof(Index));
        }

        AddErrors(result.Errors);
        vm.Id = null;
        vm.BrandOptions = _hubs.ListBrandOptions(vm.BrandIds);
        return View("HubForm", vm);
    }

    [HttpGet("Admin/Hubs/{id:int}/Edit")]
    public IActionResult EditHub(int id)
    {
        var result = _hubs.GetHub(id);
        if (!result.Ok)
            return HubMissing();

        HubDetails hub = result.Value!;
        var vm = new HubFormViewModel
        {
            Id = hub.HubId,
            Name = hub.Name,
            Address = hub.Address,
            KitchenCapacity = hub.KitchenCapacity,
            IsActive = hub.IsActive,
            BrandIds = hub.BrandIds.ToList()
        };
        FillShownOnly(vm, hub);
        return View("HubForm", vm);
    }

    [HttpPost("Admin/Hubs/{id:int}/Edit"), ValidateAntiForgeryToken]
    public IActionResult UpdateHub(int id, HubFormViewModel vm)
    {
        var current = _hubs.GetHub(id);
        if (!current.Ok)
            return HubMissing();

        var result = _hubs.UpdateHub(id, ToChanges(vm));
        if (result.Success)
        {
            TempData["Notice"] = "Hub saved.";
            return RedirectToAction(nameof(Index));
        }

        AddErrors(result.Errors);
        vm.Id = id;
        FillShownOnly(vm, current.Value!);
        return View("HubForm", vm);
    }

    // Soft delete: the hub is kept with IsActive = false.
    [HttpPost("Admin/Hubs/{id:int}/Deactivate"), ValidateAntiForgeryToken]
    public IActionResult DeactivateHub(int id)
    {
        var result = _hubs.DeactivateHub(id);
        if (result.Success)
            TempData["Notice"] = "Hub deactivated.";
        else
            TempData["Error"] = result.Errors.First();

        return RedirectToAction(nameof(Index));
    }

    // ================= helpers =================

    private static HubChanges ToChanges(HubFormViewModel vm) => new()
    {
        Name = vm.Name ?? "",
        Address = vm.Address ?? "",
        KitchenCapacity = vm.KitchenCapacity,
        BrandIds = vm.BrandIds ?? new List<int>(),
        IsActive = vm.IsActive
    };

    // The load, staff list and brand list are not typed by the Admin, so they are read again for the page.
    private void FillShownOnly(HubFormViewModel vm, HubDetails hub)
    {
        vm.Staff = hub.Staff;
        vm.Confirmed = hub.Confirmed;
        vm.Preparing = hub.Preparing;
        vm.LoadPct = hub.LoadPct;
        vm.QuietDiscountEligible = hub.QuietDiscountEligible;
        vm.BrandOptions = _hubs.ListBrandOptions(hub.BrandIds.Concat(vm.BrandIds ?? new List<int>()));
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        ModelState.Clear();
        foreach (string error in errors)
            ModelState.AddModelError(string.Empty, error);
    }

    private IActionResult HubMissing()
    {
        TempData["Error"] = "Hub not found.";
        return RedirectToAction(nameof(Index));
    }
}
