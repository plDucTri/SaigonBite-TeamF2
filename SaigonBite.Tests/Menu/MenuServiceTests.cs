using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Services;

namespace SaigonBite.Tests.Menu;

/// <summary>UC3 Manage Menu and Brands (FR15, BR12): tests T-48, T-49 and T-50 plus the filters.</summary>
public class MenuServiceTests
{
    private static readonly DateTime Day = new(2026, 11, 10, 12, 0, 0);

    // ---------- T-48: create, read, update ----------

    [Fact]
    public void T48_creates_brand_and_item_active_lists_them_and_new_price_does_not_touch_old_orders()
    {
        using var env = new MenuTestEnv();
        int brandId = env.AddBrand("Pho 24");
        int itemId = env.AddItem(brandId, "Pho Bo", "Noodles", 50000);

        // An older order that contains the item at 50,000.
        env.AddOrder(env.Hub1, OrderStatuses.Delivered, Day, lines: new[] { (itemId, 2, 50000m) });

        // Read: both are listed and active.
        BrandInfo brand = Assert.Single(env.Menu.ListBrands(null));
        Assert.True(brand.IsActive);
        Assert.Equal(1, brand.ItemCount);
        MenuItemInfo item = Assert.Single(env.Menu.ListItems(new MenuItemFilter()));
        Assert.True(item.IsActive);
        Assert.True(item.IsAvailable);
        Assert.Equal(50000m, item.Price);

        // Update: 50,000 -> 55,000.
        var update = env.Menu.UpdateItem(itemId, MenuTestEnv.Item(brandId, "Pho Bo", "Noodles", 55000));
        Assert.True(update.Success);

        Assert.Equal(55000m, env.Menu.GetItem(itemId).Value!.Price);   // new orders use 55,000
        env.Db.ChangeTracker.Clear();
        Assert.Equal(50000m, env.Db.OrderLines.Single().UnitPrice);    // the older order line keeps 50,000
    }

    [Fact]
    public void Update_changes_name_category_and_availability()
    {
        using var env = new MenuTestEnv();
        int brandId = env.AddBrand("Pho 24");
        int itemId = env.AddItem(brandId, "Pho Bo", "Noodles", 65000);

        var result = env.Menu.UpdateItem(itemId, MenuTestEnv.Item(brandId, "Pho Tai", "rice", 70000, available: false));

        Assert.True(result.Success);
        MenuItemInfo item = env.Menu.GetItem(itemId).Value!;
        Assert.Equal("Pho Tai", item.Name);
        Assert.Equal("Rice", item.Category);          // stored as spelled in the list
        Assert.Equal(70000m, item.Price);
        Assert.False(item.IsAvailable);
    }

    [Fact]
    public void Update_brand_changes_name_and_status()
    {
        using var env = new MenuTestEnv();
        int brandId = env.AddBrand("Pho 24");

        Assert.True(env.Menu.UpdateBrand(brandId, new BrandChanges { Name = "  Pho 25 ", IsActive = false }).Success);

        BrandInfo brand = env.Menu.GetBrand(brandId).Value!;
        Assert.Equal("Pho 25", brand.Name);           // trimmed
        Assert.False(brand.IsActive);
    }

    [Fact]
    public void A_brand_can_be_saved_with_its_own_name_again()
    {
        using var env = new MenuTestEnv();
        int brandId = env.AddBrand("Pho 24");

        Assert.True(env.Menu.UpdateBrand(brandId, new BrandChanges { Name = "Pho 24", IsActive = true }).Success);
    }

    // ---------- T-49: soft delete ----------

    [Fact]
    public async Task T49_removing_an_item_keeps_the_row_hides_it_from_ordering_and_keeps_old_orders()
    {
        using var env = new MenuTestEnv();
        int brandId = env.AddBrand("Pho 24");
        int itemId = env.AddItem(brandId);
        env.AddItem(brandId, "Pho Ga", "Noodles", 70000);
        env.AddOrder(env.Hub1, OrderStatuses.Delivered, Day, lines: new[] { (itemId, 1, 50000m) });

        Assert.Equal(2, (await env.Catalog.GetHubMenuAsync(env.Hub1)).Count);

        Assert.True(env.Menu.RemoveItem(itemId).Success);

        env.Db.ChangeTracker.Clear();
        MenuItem row = env.Db.MenuItems.Single(m => m.ItemId == itemId);     // row kept
        Assert.False(row.IsActive);
        Assert.Equal(50000m, row.Price);

        var menu = await env.Catalog.GetHubMenuAsync(env.Hub1);              // can no longer be ordered
        Assert.DoesNotContain(menu, m => m.ItemId == itemId);
        Assert.Single(menu);

        Assert.Single(env.Db.OrderLines.Where(l => l.ItemId == itemId));     // the old order still shows it
        Assert.DoesNotContain(env.Menu.ListItems(new MenuItemFilter()), m => m.ItemId == itemId);
    }

    [Fact]
    public async Task T49_deactivating_a_brand_that_has_items_keeps_all_rows_and_stops_ordering()
    {
        using var env = new MenuTestEnv();
        int brandId = env.AddBrand("Pho 24");
        int itemId = env.AddItem(brandId);
        env.AddOrder(env.Hub1, OrderStatuses.Delivered, Day, lines: new[] { (itemId, 1, 50000m) });

        Assert.True(env.Menu.DeactivateBrand(brandId).Success);

        env.Db.ChangeTracker.Clear();
        Assert.False(env.Db.Brands.Single(b => b.BrandId == brandId).IsActive);   // brand row kept
        Assert.True(env.Db.MenuItems.Single(m => m.ItemId == itemId).IsActive);   // item row untouched, so the menu comes back with the brand
        Assert.Empty(await env.Catalog.GetHubMenuAsync(env.Hub1));                // but nothing of it can be ordered
        Assert.Single(env.Db.OrderLines);                                          // old order untouched

        // The brand is still listed (as Inactive) for the Admin.
        Assert.False(Assert.Single(env.Menu.ListBrands(null)).IsActive);
    }

    [Fact]
    public async Task Switching_availability_off_hides_the_item_from_ordering_but_keeps_it_in_the_list()
    {
        using var env = new MenuTestEnv();
        int brandId = env.AddBrand("Pho 24");
        int itemId = env.AddItem(brandId);

        Assert.True(env.Menu.UpdateItem(itemId, MenuTestEnv.Item(brandId, available: false)).Success);

        Assert.Empty(await env.Catalog.GetHubMenuAsync(env.Hub1));
        Assert.Single(env.Menu.ListItems(new MenuItemFilter()));
    }

    [Fact]
    public void Removing_twice_and_editing_a_removed_item_are_refused()
    {
        using var env = new MenuTestEnv();
        int brandId = env.AddBrand("Pho 24");
        int itemId = env.AddItem(brandId);
        Assert.True(env.Menu.RemoveItem(itemId).Success);

        Assert.False(env.Menu.RemoveItem(itemId).Success);
        Assert.False(env.Menu.UpdateItem(itemId, MenuTestEnv.Item(brandId, price: 60000)).Success);
    }

    [Fact]
    public void Deactivating_a_brand_twice_and_unknown_ids_are_refused()
    {
        using var env = new MenuTestEnv();
        int brandId = env.AddBrand("Pho 24");
        Assert.True(env.Menu.DeactivateBrand(brandId).Success);

        Assert.False(env.Menu.DeactivateBrand(brandId).Success);
        Assert.False(env.Menu.DeactivateBrand(999).Success);
        Assert.False(env.Menu.RemoveItem(999).Success);
        Assert.False(env.Menu.GetBrand(999).Ok);
        Assert.False(env.Menu.GetItem(999).Ok);
    }

    // ---------- T-50: invalid data is refused and nothing is saved ----------

    [Theory]
    [InlineData("", "Noodles", 50000)]          // empty name
    [InlineData("   ", "Noodles", 50000)]       // blank name
    [InlineData("Pho Bo", "Noodles", 0)]        // price 0
    [InlineData("Pho Bo", "Noodles", -1)]       // negative price
    [InlineData("Pho Bo", "Pizza", 50000)]      // category not in the list
    [InlineData("Pho Bo", "", 50000)]           // no category
    public void T50_invalid_item_is_rejected_and_nothing_is_saved(string name, string category, int price)
    {
        using var env = new MenuTestEnv();
        int brandId = env.AddBrand("Pho 24");

        var result = env.Menu.CreateItem(MenuTestEnv.Item(brandId, name, category, price));

        Assert.False(result.Ok);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(env.Menu.ListItems(new MenuItemFilter()));
        Assert.Equal(0, env.Db.MenuItems.Count());
    }

    [Fact]
    public void T50_a_missing_price_and_a_missing_brand_are_rejected()
    {
        using var env = new MenuTestEnv();
        int brandId = env.AddBrand("Pho 24");

        Assert.False(env.Menu.CreateItem(MenuTestEnv.Item(brandId, price: null)).Ok);
        Assert.False(env.Menu.CreateItem(MenuTestEnv.Item(0)).Ok);
        Assert.False(env.Menu.CreateItem(MenuTestEnv.Item(999)).Ok);
        Assert.Equal(0, env.Db.MenuItems.Count());
    }

    [Theory]
    [InlineData("Pho 24")]
    [InlineData("pho 24")]       // letter case does not matter
    [InlineData("  Pho 24  ")]   // neither do spaces around the name
    public void T50_a_brand_name_that_already_exists_is_rejected(string name)
    {
        using var env = new MenuTestEnv();
        env.AddBrand("Pho 24");

        var result = env.Menu.CreateBrand(new BrandChanges { Name = name });

        Assert.False(result.Ok);
        Assert.Equal(1, env.Db.Brands.Count());
    }

    [Fact]
    public void T50_an_inactive_brand_still_blocks_its_name()
    {
        using var env = new MenuTestEnv();
        env.AddBrand("Pho 24", active: false);

        Assert.False(env.Menu.CreateBrand(new BrandChanges { Name = "Pho 24" }).Ok);
    }

    [Fact]
    public void T50_renaming_a_brand_to_the_name_of_another_brand_is_rejected()
    {
        using var env = new MenuTestEnv();
        env.AddBrand("Pho 24");
        int second = env.AddBrand("Drinks Hub");

        var result = env.Menu.UpdateBrand(second, new BrandChanges { Name = "pho 24" });

        Assert.False(result.Success);
        Assert.Equal("Drinks Hub", env.Menu.GetBrand(second).Value!.Name);
    }

    [Fact]
    public void T50_an_empty_brand_name_is_rejected()
    {
        using var env = new MenuTestEnv();

        Assert.False(env.Menu.CreateBrand(new BrandChanges { Name = "" }).Ok);
        Assert.False(env.Menu.CreateBrand(new BrandChanges { Name = new string('x', 101) }).Ok);
        Assert.Equal(0, env.Db.Brands.Count());
    }

    [Fact]
    public void An_item_needs_an_active_brand_and_a_name_that_is_new_for_that_brand()
    {
        using var env = new MenuTestEnv();
        int active = env.AddBrand("Pho 24");
        int inactive = env.AddBrand("Old Brand", active: false);
        env.AddItem(active, "Pho Bo");

        Assert.False(env.Menu.CreateItem(MenuTestEnv.Item(inactive)).Ok);                  // inactive brand
        Assert.False(env.Menu.CreateItem(MenuTestEnv.Item(active, "pho bo")).Ok);          // same name, same brand
        // the same name in another brand is fine
        int other = env.AddBrand("Banh Mi Saigon");
        Assert.True(env.Menu.CreateItem(MenuTestEnv.Item(other, "Pho Bo")).Ok);
    }

    [Fact]
    public void A_removed_item_does_not_block_its_name_and_an_item_of_an_inactive_brand_can_still_be_saved()
    {
        using var env = new MenuTestEnv();
        int brandId = env.AddBrand("Pho 24");
        int itemId = env.AddItem(brandId, "Pho Bo");
        Assert.True(env.Menu.RemoveItem(itemId).Success);
        Assert.True(env.Menu.CreateItem(MenuTestEnv.Item(brandId, "Pho Bo")).Ok);   // name is free again

        int second = env.AddItem(brandId, "Pho Ga");
        Assert.True(env.Menu.DeactivateBrand(brandId).Success);
        // Staying in the (now inactive) brand is allowed; moving to an inactive brand is not.
        Assert.True(env.Menu.UpdateItem(second, MenuTestEnv.Item(brandId, "Pho Ga", price: 72000)).Success);
        int inactiveOther = env.AddBrand("Other", active: false);
        Assert.False(env.Menu.UpdateItem(second, MenuTestEnv.Item(inactiveOther, "Pho Ga")).Success);
    }

    // ---------- Figma A02 filters ----------

    [Fact]
    public void The_menu_item_filters_work_for_hub_brand_category_and_availability()
    {
        using var env = new MenuTestEnv();
        int pho = env.AddBrand("Pho 24");                          // operated by Hub 1
        int drinks = env.AddBrand("Drinks Hub", atHub1: false);    // operated by no hub
        env.AddItem(pho, "Pho Bo", "Noodles", 65000);
        env.AddItem(pho, "Pho Ga", "Noodles", 70000, available: false);
        env.AddItem(drinks, "Tra Dao", "Drinks", 30000);

        Assert.Equal(3, env.Menu.ListItems(new MenuItemFilter()).Count);
        Assert.Equal(2, env.Menu.ListItems(new MenuItemFilter { HubId = env.Hub1 }).Count);
        Assert.Empty(env.Menu.ListItems(new MenuItemFilter { HubId = env.Hub2 }));
        Assert.Single(env.Menu.ListItems(new MenuItemFilter { BrandId = drinks }));
        Assert.Equal(2, env.Menu.ListItems(new MenuItemFilter { Category = "Noodles" }).Count);
        Assert.Equal("Pho Ga", Assert.Single(env.Menu.ListItems(new MenuItemFilter { Availability = AvailabilityFilter.Unavailable })).Name);
        Assert.Equal(2, env.Menu.ListItems(new MenuItemFilter { Availability = AvailabilityFilter.Available }).Count);
    }

    [Fact]
    public void The_brand_list_shows_item_count_hubs_and_search()
    {
        using var env = new MenuTestEnv();
        int pho = env.AddBrand("Pho 24");
        env.AddBrand("Drinks Hub", atHub1: false);
        env.AddItem(pho, "Pho Bo");
        int removed = env.AddItem(pho, "Pho Ga");
        env.Menu.RemoveItem(removed);                            // removed items are not counted

        BrandInfo brand = env.Menu.ListBrands("pho").Single();
        Assert.Equal("Pho 24", brand.Name);
        Assert.Equal(1, brand.ItemCount);
        Assert.Equal(new[] { "Hub 1" }, brand.HubNames);
        Assert.Equal(2, env.Menu.ListBrands(null).Count);
    }

    [Fact]
    public void ListHubs_gives_the_active_hubs()
    {
        using var env = new MenuTestEnv();
        Assert.Equal(new[] { "Hub 1", "Hub 2" }, env.Menu.ListHubs().Select(h => h.Name).ToArray());
    }
}
