using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaigonBite.Helpers;
using SaigonBite.Services;
using SaigonBite.ViewModels;

namespace SaigonBite.Controllers;

/// <summary>
/// Admin Portal: UC3 Manage Menu and Brands (FR15). Only the Admin role may use it (NFR4).
/// Like the other admin controllers it only passes data between the pages and IMenuService:
/// there are no menu rules in this class (NFR2).
/// </summary>
[Authorize(Roles = Roles.Admin)]
public class MenuController : Controller
{
    private readonly IMenuService _menu;

    public MenuController(IMenuService menu)
    {
        _menu = menu;
    }

    // ================= Brands (Figma A01, A10) =================

    [HttpGet("Admin/Brands")]
    public IActionResult Brands(string? search)
    {
        return View(new BrandListViewModel { Search = search, Brands = _menu.ListBrands(search) });
    }

    [HttpGet("Admin/Brands/{id:int}")]
    public IActionResult BrandDetails(int id)
    {
        var result = _menu.GetBrand(id);
        if (!result.Ok)
            return BrandMissing();

        return View(result.Value);
    }

    [HttpGet("Admin/Brands/New")]
    public IActionResult NewBrand()
    {
        return View("BrandForm", new BrandFormViewModel());
    }

    [HttpPost("Admin/Brands/New"), ValidateAntiForgeryToken]
    public IActionResult CreateBrand(BrandFormViewModel vm)
    {
        var result = _menu.CreateBrand(new BrandChanges { Name = vm.Name ?? "", IsActive = vm.IsActive });
        if (result.Ok)
        {
            TempData["Notice"] = "Brand saved.";
            return RedirectToAction(nameof(Brands));
        }

        AddErrors(result.Errors);
        vm.Id = null;
        return View("BrandForm", vm);
    }

    [HttpGet("Admin/Brands/{id:int}/Edit")]
    public IActionResult EditBrand(int id)
    {
        var result = _menu.GetBrand(id);
        if (!result.Ok)
            return BrandMissing();

        BrandInfo brand = result.Value!;
        return View("BrandForm", new BrandFormViewModel
        {
            Id = brand.BrandId,
            Name = brand.Name,
            IsActive = brand.IsActive,
            HubNames = brand.HubNames
        });
    }

    [HttpPost("Admin/Brands/{id:int}/Edit"), ValidateAntiForgeryToken]
    public IActionResult UpdateBrand(int id, BrandFormViewModel vm)
    {
        var current = _menu.GetBrand(id);
        if (!current.Ok)
            return BrandMissing();

        var result = _menu.UpdateBrand(id, new BrandChanges { Name = vm.Name ?? "", IsActive = vm.IsActive });
        if (result.Success)
        {
            TempData["Notice"] = "Brand saved.";
            return RedirectToAction(nameof(Brands));
        }

        AddErrors(result.Errors);
        vm.Id = id;
        vm.HubNames = current.Value!.HubNames;
        return View("BrandForm", vm);
    }

    // Soft delete: the brand is kept with IsActive = false.
    [HttpPost("Admin/Brands/{id:int}/Deactivate"), ValidateAntiForgeryToken]
    public IActionResult DeactivateBrand(int id)
    {
        var result = _menu.DeactivateBrand(id);
        if (result.Success)
            TempData["Notice"] = "Brand deactivated.";
        else
            TempData["Error"] = result.Errors.First();

        return RedirectToAction(nameof(Brands));
    }

    // ================= Menu items (Figma A02, A03) =================

    [HttpGet("Admin/MenuItems")]
    public IActionResult MenuItems(int? hubId, int? brandId, string? category, string? availability)
    {
        var filter = new MenuItemFilter
        {
            HubId = hubId,
            BrandId = brandId,
            Category = category,
            Availability = availability
        };

        return View(new MenuItemListViewModel
        {
            HubId = hubId,
            BrandId = brandId,
            Category = category,
            Availability = availability,
            Hubs = _menu.ListHubs(),
            Brands = _menu.ListBrands(null).Where(b => b.IsActive).ToList(),
            Items = _menu.ListItems(filter)
        });
    }

    [HttpGet("Admin/MenuItems/{id:int}")]
    public IActionResult ItemDetails(int id)
    {
        var result = _menu.GetItem(id);
        if (!result.Ok)
            return ItemMissing();

        return View(result.Value);
    }

    [HttpGet("Admin/MenuItems/New")]
    public IActionResult NewItem()
    {
        return View("ItemForm", FillBrands(new MenuItemFormViewModel(), null));
    }

    [HttpPost("Admin/MenuItems/New"), ValidateAntiForgeryToken]
    public IActionResult CreateItem(MenuItemFormViewModel vm)
    {
        var result = _menu.CreateItem(ToChanges(vm));
        if (result.Ok)
        {
            TempData["Notice"] = "Menu item saved.";
            return RedirectToAction(nameof(MenuItems));
        }

        AddErrors(result.Errors);
        vm.Id = null;
        return View("ItemForm", FillBrands(vm, null));
    }

    [HttpGet("Admin/MenuItems/{id:int}/Edit")]
    public IActionResult EditItem(int id)
    {
        var result = _menu.GetItem(id);
        if (!result.Ok)
            return ItemMissing();

        MenuItemInfo item = result.Value!;
        if (!item.IsActive)
            return ItemMissing();   // a removed item cannot be edited

        var vm = new MenuItemFormViewModel
        {
            Id = item.ItemId,
            Name = item.Name,
            BrandId = item.BrandId,
            Category = item.Category,
            Price = item.Price,
            IsAvailable = item.IsAvailable
        };
        return View("ItemForm", FillBrands(vm, item.BrandId));
    }

    [HttpPost("Admin/MenuItems/{id:int}/Edit"), ValidateAntiForgeryToken]
    public IActionResult UpdateItem(int id, MenuItemFormViewModel vm)
    {
        var current = _menu.GetItem(id);
        if (!current.Ok)
            return ItemMissing();

        var result = _menu.UpdateItem(id, ToChanges(vm));
        if (result.Success)
        {
            TempData["Notice"] = "Menu item saved.";
            return RedirectToAction(nameof(MenuItems));
        }

        AddErrors(result.Errors);
        vm.Id = id;
        return View("ItemForm", FillBrands(vm, current.Value!.BrandId));
    }

    // Soft delete: the item is kept with IsActive = false, so old orders and reports still show it.
    [HttpPost("Admin/MenuItems/{id:int}/Remove"), ValidateAntiForgeryToken]
    public IActionResult RemoveItem(int id)
    {
        var result = _menu.RemoveItem(id);
        if (result.Success)
            TempData["Notice"] = "Menu item removed.";
        else
            TempData["Error"] = result.Errors.First();

        return RedirectToAction(nameof(MenuItems));
    }

    // ================= helpers =================

    private static MenuItemChanges ToChanges(MenuItemFormViewModel vm) => new()
    {
        Name = vm.Name ?? "",
        BrandId = vm.BrandId,
        Category = vm.Category ?? "",
        Price = vm.Price,
        IsAvailable = vm.IsAvailable
    };

    // The brand drop-down lists the active brands, plus the brand the item already has (even if it is inactive now).
    private MenuItemFormViewModel FillBrands(MenuItemFormViewModel vm, int? currentBrandId)
    {
        vm.Brands = _menu.ListBrands(null)
            .Where(b => b.IsActive || b.BrandId == currentBrandId)
            .ToList();
        return vm;
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        ModelState.Clear();
        foreach (string error in errors)
            ModelState.AddModelError(string.Empty, error);
    }

    private IActionResult BrandMissing()
    {
        TempData["Error"] = "Brand not found.";
        return RedirectToAction(nameof(Brands));
    }

    private IActionResult ItemMissing()
    {
        TempData["Error"] = "Menu item not found.";
        return RedirectToAction(nameof(MenuItems));
    }
}
