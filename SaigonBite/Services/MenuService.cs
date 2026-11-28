using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Repositories;
using SaigonBite.Validators;

namespace SaigonBite.Services;

/// <summary>
/// UC3 business layer. It checks the input with IMenuValidator, applies the rules that need the data
/// (unique brand name, active brand) and saves through the repositories. Register as scoped.
/// </summary>
public class MenuService : IMenuService
{
    private readonly IBrandRepository _brands;
    private readonly IMenuItemRepository _items;
    private readonly IHubRepository _hubs;
    private readonly IMenuValidator _validator;

    public MenuService(IBrandRepository brands, IMenuItemRepository items, IHubRepository hubs, IMenuValidator validator)
    {
        _brands = brands;
        _items = items;
        _hubs = hubs;
        _validator = validator;
    }

    // ================= Brands =================

    public IReadOnlyList<BrandInfo> ListBrands(string? search) =>
        _brands.GetAll(search).Select(x => ToInfo(x)).ToList();

    public ServiceResult<BrandInfo> GetBrand(int brandId)
    {
        Brand? brand = _brands.Find(brandId);
        return brand is null
            ? ServiceResult<BrandInfo>.Fail("Brand not found.")
            : ServiceResult<BrandInfo>.Success(ToInfo(brand));
    }

    public ServiceResult<int> CreateBrand(BrandChanges changes)
    {
        var errors = _validator.ValidateBrand(changes).ToList();
        if (errors.Count > 0)
            return ServiceResult<int>.Fail(errors);

        string name = changes.Name.Trim();
        if (_brands.NameExists(name, null))
            return ServiceResult<int>.Fail("A brand with this name already exists.");

        var brand = new Brand { Name = name, IsActive = changes.IsActive };
        _brands.Add(brand);
        _brands.Save();
        return ServiceResult<int>.Success(brand.BrandId);
    }

    public OperationResult UpdateBrand(int brandId, BrandChanges changes)
    {
        Brand? brand = _brands.Find(brandId);
        if (brand is null)
            return OperationResult.Fail("Brand not found.");

        var errors = _validator.ValidateBrand(changes).ToList();
        if (errors.Count > 0)
            return OperationResult.Fail(errors.ToArray());

        string name = changes.Name.Trim();
        if (_brands.NameExists(name, brandId))
            return OperationResult.Fail("A brand with this name already exists.");

        brand.Name = name;
        brand.IsActive = changes.IsActive;
        _brands.Save();
        return OperationResult.Ok();
    }

    public OperationResult DeactivateBrand(int brandId)
    {
        Brand? brand = _brands.Find(brandId);
        if (brand is null)
            return OperationResult.Fail("Brand not found.");
        if (!brand.IsActive)
            return OperationResult.Fail("This brand is already inactive.");

        // Only the brand is switched off. Its items keep their own flags, so switching the brand on again
        // brings the menu back. OrderCatalogRepository.GetHubMenuAsync already hides items of inactive brands (BR12).
        brand.IsActive = false;
        _brands.Save();
        return OperationResult.Ok();
    }

    // ================= Menu items =================

    public IReadOnlyList<MenuItemInfo> ListItems(MenuItemFilter filter) =>
        _items.GetActive(filter ?? new MenuItemFilter()).Select(x => ToInfo(x)).ToList();

    public ServiceResult<MenuItemInfo> GetItem(int itemId)
    {
        MenuItem? item = _items.Find(itemId);
        return item is null
            ? ServiceResult<MenuItemInfo>.Fail("Menu item not found.")
            : ServiceResult<MenuItemInfo>.Success(ToInfo(item));
    }

    public ServiceResult<int> CreateItem(MenuItemChanges changes)
    {
        var errors = _validator.ValidateItem(changes).ToList();
        if (errors.Count > 0)
            return ServiceResult<int>.Fail(errors);

        Brand? brand = _brands.Find(changes.BrandId);
        if (brand is null || !brand.IsActive)
            return ServiceResult<int>.Fail("Choose an active brand.");

        string name = changes.Name.Trim();
        if (_items.NameExists(brand.BrandId, name, null))
            return ServiceResult<int>.Fail("This brand already has a menu item with this name.");

        var item = new MenuItem
        {
            BrandId = brand.BrandId,
            Name = name,
            Category = MenuCategories.Find(changes.Category)!,
            Price = changes.Price!.Value,
            IsAvailable = changes.IsAvailable,
            IsActive = true
        };
        _items.Add(item);
        _items.Save();
        return ServiceResult<int>.Success(item.ItemId);
    }

    public OperationResult UpdateItem(int itemId, MenuItemChanges changes)
    {
        MenuItem? item = _items.Find(itemId);
        if (item is null)
            return OperationResult.Fail("Menu item not found.");
        if (!item.IsActive)
            return OperationResult.Fail("A removed menu item cannot be edited.");

        var errors = _validator.ValidateItem(changes).ToList();
        if (errors.Count > 0)
            return OperationResult.Fail(errors.ToArray());

        // The brand may stay as it is (even if it is inactive now); moving the item needs an active brand.
        if (changes.BrandId != item.BrandId)
        {
            Brand? newBrand = _brands.Find(changes.BrandId);
            if (newBrand is null || !newBrand.IsActive)
                return OperationResult.Fail("Choose an active brand.");
        }

        string name = changes.Name.Trim();
        if (_items.NameExists(changes.BrandId, name, itemId))
            return OperationResult.Fail("This brand already has a menu item with this name.");

        item.BrandId = changes.BrandId;
        item.Name = name;
        item.Category = MenuCategories.Find(changes.Category)!;
        item.Price = changes.Price!.Value;       // order lines keep their own UnitPrice, so old orders do not change
        item.IsAvailable = changes.IsAvailable;
        _items.Save();
        return OperationResult.Ok();
    }

    public OperationResult RemoveItem(int itemId)
    {
        MenuItem? item = _items.Find(itemId);
        if (item is null)
            return OperationResult.Fail("Menu item not found.");
        if (!item.IsActive)
            return OperationResult.Fail("This menu item has already been removed.");

        item.IsActive = false;
        _items.Save();
        return OperationResult.Ok();
    }

    public IReadOnlyList<HubOption> ListHubs() =>
        _hubs.GetActiveHubs().Select(h => new HubOption(h.HubId, h.Name)).ToList();

    // ================= Mapping =================

    private static BrandInfo ToInfo(Brand b) =>
        new(b.BrandId, b.Name, b.IsActive,
            b.MenuItems.Count(i => i.IsActive),
            b.Hubs.Select(h => h.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList());

    private static MenuItemInfo ToInfo(MenuItem m) =>
        new(m.ItemId, m.BrandId, m.Brand.Name, m.Brand.IsActive, m.Name, m.Category, m.Price, m.IsAvailable, m.IsActive);
}
