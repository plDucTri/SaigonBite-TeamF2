namespace SaigonBite.Services;

/// <summary>
/// UC3 Manage Menu and Brands (FR15). Create, read, update and soft-delete brands and menu items,
/// including the availability switch. Nothing is ever deleted from the database: removing sets IsActive = false,
/// so old orders and reports still show the item (BR9 style, tests T-48 to T-50).
/// </summary>
public interface IMenuService
{
    // ----- Brands (Figma A01, A10) -----
    IReadOnlyList<BrandInfo> ListBrands(string? search);
    ServiceResult<BrandInfo> GetBrand(int brandId);
    ServiceResult<int> CreateBrand(BrandChanges changes);
    OperationResult UpdateBrand(int brandId, BrandChanges changes);

    /// <summary>Soft delete. Its items are kept; they cannot be ordered while the brand is inactive (BR12).</summary>
    OperationResult DeactivateBrand(int brandId);

    // ----- Menu items (Figma A02, A03) -----
    IReadOnlyList<MenuItemInfo> ListItems(MenuItemFilter filter);
    ServiceResult<MenuItemInfo> GetItem(int itemId);
    ServiceResult<int> CreateItem(MenuItemChanges changes);

    /// <summary>A new price applies to new orders only: existing order lines keep the price saved at placement (BR17).</summary>
    OperationResult UpdateItem(int itemId, MenuItemChanges changes);

    /// <summary>Soft delete.</summary>
    OperationResult RemoveItem(int itemId);

    /// <summary>Active hubs, for the hub filter of the Menu Items screen.</summary>
    IReadOnlyList<HubOption> ListHubs();
}
