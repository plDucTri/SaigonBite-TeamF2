using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Repositories;
using SaigonBite.Services;

namespace SaigonBite.Tests.Menu;

/// <summary>Admin Hubs and Capacity (FR17, BR7, BR8): create, edit, brands operated, load figures and soft delete.</summary>
public class HubServiceTests
{
    private static readonly DateTime Day = new(2026, 11, 10, 12, 0, 0);

    private static HubChanges Changes(string name = "Binh Thanh", int? capacity = 20, params int[] brandIds) => new()
    {
        Name = name,
        Address = "Binh Thanh District, Ho Chi Minh City",
        KitchenCapacity = capacity,
        BrandIds = brandIds.ToList()
    };

    // ---------- create and read ----------

    [Fact]
    public void Creating_a_hub_saves_it_active_with_its_brands()
    {
        using var env = new MenuTestEnv();
        int pho = env.AddBrand("Pho 24", atHub1: false);
        int drinks = env.AddBrand("Drinks Hub", atHub1: false);

        var created = env.Hubs.CreateHub(Changes("Binh Thanh", 20, pho, drinks));

        Assert.True(created.Ok, string.Join(" | ", created.Errors));
        HubDetails hub = env.Hubs.GetHub(created.Value).Value!;
        Assert.Equal("Binh Thanh", hub.Name);
        Assert.Equal(20, hub.KitchenCapacity);
        Assert.True(hub.IsActive);
        Assert.Equal(new[] { "Drinks Hub", "Pho 24" }, hub.BrandNames.ToArray());
    }

    [Theory]
    [InlineData("", 20)]
    [InlineData("Binh Thanh", 0)]
    [InlineData("Binh Thanh", null)]
    [InlineData("Binh Thanh", 1001)]
    public void Invalid_hub_data_is_rejected_and_nothing_is_saved(string name, int? capacity)
    {
        using var env = new MenuTestEnv();
        int before = env.Db.Hubs.Count();

        var result = env.Hubs.CreateHub(Changes(name, capacity));

        Assert.False(result.Ok);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(before, env.Db.Hubs.Count());
    }

    [Theory]
    [InlineData("Hub 1")]
    [InlineData("hub 1")]       // letter case does not matter
    [InlineData("  Hub 1  ")]   // neither do spaces around the name
    public void A_hub_name_that_already_exists_is_rejected(string name)
    {
        using var env = new MenuTestEnv();   // already has "Hub 1" and "Hub 2"

        Assert.False(env.Hubs.CreateHub(Changes(name)).Ok);
        Assert.Equal(2, env.Db.Hubs.Count());
    }

    [Fact]
    public void A_missing_or_inactive_brand_is_rejected()
    {
        using var env = new MenuTestEnv();
        int inactive = env.AddBrand("Old Brand", atHub1: false, active: false);

        Assert.False(env.Hubs.CreateHub(Changes("New Hub", 20, 999)).Ok);       // no such brand
        Assert.False(env.Hubs.CreateHub(Changes("New Hub", 20, inactive)).Ok);  // inactive brand
        Assert.Equal(2, env.Db.Hubs.Count());
    }

    // ---------- edit ----------

    [Fact]
    public void Editing_changes_name_address_capacity_and_the_brands_operated()
    {
        using var env = new MenuTestEnv();
        int pho = env.AddBrand("Pho 24", atHub1: false);
        int drinks = env.AddBrand("Drinks Hub", atHub1: false);
        int hubId = env.Hubs.CreateHub(Changes("Binh Thanh", 20, pho)).Value;

        var update = env.Hubs.UpdateHub(hubId, new HubChanges
        {
            Name = "Binh Thanh 2", Address = "New address", KitchenCapacity = 30,
            BrandIds = new List<int> { drinks }, IsActive = true
        });

        Assert.True(update.Success);
        HubDetails hub = env.Hubs.GetHub(hubId).Value!;
        Assert.Equal("Binh Thanh 2", hub.Name);
        Assert.Equal("New address", hub.Address);
        Assert.Equal(30, hub.KitchenCapacity);
        Assert.Equal(new[] { "Drinks Hub" }, hub.BrandNames.ToArray());   // Pho 24 was unticked
    }

    [Fact]
    public async Task The_brands_operated_decide_which_menu_a_customer_sees_at_the_hub()
    {
        using var env = new MenuTestEnv();
        int pho = env.AddBrand("Pho 24", atHub1: false);
        env.AddItem(pho);
        int hubId = env.Hubs.CreateHub(Changes("Binh Thanh", 20)).Value;     // operates nothing yet

        Assert.Empty(await env.Catalog.GetHubMenuAsync(hubId));

        Assert.True(env.Hubs.UpdateHub(hubId, Changes("Binh Thanh", 20, pho)).Success);
        Assert.Single(await env.Catalog.GetHubMenuAsync(hubId));

        Assert.True(env.Hubs.UpdateHub(hubId, Changes("Binh Thanh", 20)).Success);
        Assert.Empty(await env.Catalog.GetHubMenuAsync(hubId));
    }

    [Fact]
    public void A_hub_can_be_saved_with_its_own_name_and_cannot_take_the_name_of_another_hub()
    {
        using var env = new MenuTestEnv();

        Assert.True(env.Hubs.UpdateHub(env.Hub1, Changes("Hub 1", 10)).Success);
        Assert.False(env.Hubs.UpdateHub(env.Hub1, Changes("hub 2", 10)).Success);
        Assert.Equal("Hub 1", env.Hubs.GetHub(env.Hub1).Value!.Name);
    }

    [Fact]
    public void An_inactive_brand_that_is_already_operated_may_stay_but_cannot_be_added()
    {
        using var env = new MenuTestEnv();
        int brand = env.AddBrand("Pho 24");                  // operated by Hub 1
        Assert.True(env.Menu.DeactivateBrand(brand).Success);
        int other = env.AddBrand("Other", atHub1: false, active: false);

        Assert.True(env.Hubs.UpdateHub(env.Hub1, Changes("Hub 1", 10, brand)).Success);          // stays
        Assert.False(env.Hubs.UpdateHub(env.Hub1, Changes("Hub 1", 10, brand, other)).Success);  // new inactive brand
    }

    [Fact]
    public void Unknown_ids_are_refused()
    {
        using var env = new MenuTestEnv();

        Assert.False(env.Hubs.GetHub(999).Ok);
        Assert.False(env.Hubs.UpdateHub(999, Changes()).Success);
        Assert.False(env.Hubs.DeactivateHub(999).Success);
    }

    // ---------- load figures (Figma A04, A09; BR7, BR8) ----------

    [Fact]
    public void The_list_shows_confirmed_preparing_and_load_for_each_hub()
    {
        using var env = new MenuTestEnv();   // Hub 1 capacity 10, Hub 2 capacity 20
        for (int i = 0; i < 2; i++) env.AddOrder(env.Hub1, OrderStatuses.Confirmed, Day);
        for (int i = 0; i < 2; i++) env.AddOrder(env.Hub1, OrderStatuses.Preparing, Day);
        env.AddOrder(env.Hub1, OrderStatuses.Ready, Day);        // not load
        env.AddOrder(env.Hub1, OrderStatuses.Delivered, Day);    // not load

        IReadOnlyList<HubSummary> list = env.Hubs.ListHubs();

        HubSummary hub1 = list.Single(h => h.HubId == env.Hub1);
        Assert.Equal(2, hub1.Confirmed);
        Assert.Equal(2, hub1.Preparing);
        Assert.Equal(40m, hub1.LoadPct);          // 4 / 10

        HubSummary hub2 = list.Single(h => h.HubId == env.Hub2);
        Assert.Equal(0, hub2.Confirmed);
        Assert.Equal(0, hub2.Preparing);
        Assert.Equal(0m, hub2.LoadPct);
    }

    [Theory]
    [InlineData(29, true)]     // 29%: below 30%, eligible
    [InlineData(30, false)]    // 30%: not eligible
    [InlineData(31, false)]
    public void Quiet_discount_eligibility_follows_the_30_percent_threshold(int activeOrders, bool eligible)
    {
        using var env = new MenuTestEnv();
        int hubId = env.Hubs.CreateHub(Changes("Test Hub 100", 100)).Value;
        for (int i = 0; i < activeOrders; i++) env.AddOrder(hubId, OrderStatuses.Preparing, Day);

        HubDetails hub = env.Hubs.GetHub(hubId).Value!;

        Assert.Equal((decimal)activeOrders, hub.LoadPct);
        Assert.Equal(eligible, hub.QuietDiscountEligible);
    }

    [Fact]
    public void Lowering_the_capacity_raises_the_load_but_does_not_touch_existing_orders()
    {
        using var env = new MenuTestEnv();
        for (int i = 0; i < 5; i++) env.AddOrder(env.Hub1, OrderStatuses.Preparing, Day);   // 5 / 10 = 50%

        Assert.True(env.Hubs.UpdateHub(env.Hub1, Changes("Hub 1", 5)).Success);                // 5 / 5 = 100%

        Assert.Equal(100m, env.Hubs.GetHub(env.Hub1).Value!.LoadPct);
        Assert.Equal(5, env.Db.Orders.Count(o => o.Status == OrderStatuses.Preparing));
    }

    // ---------- staff ----------

    [Fact]
    public void The_edit_page_lists_active_kitchen_staff_of_the_hub_only()
    {
        using var env = new MenuTestEnv();
        env.Db.Users.AddRange(
            new User { Identifier = "STAFF-001", PasswordHash = "x", Role = Roles.Kitchen, FullName = "Tran Thi Mai", IsActive = true, HubId = env.Hub1, CreatedAt = Day },
            new User { Identifier = "STAFF-002", PasswordHash = "x", Role = Roles.Kitchen, FullName = "Left Company", IsActive = false, HubId = env.Hub1, CreatedAt = Day },
            new User { Identifier = "STAFF-003", PasswordHash = "x", Role = Roles.Kitchen, FullName = "Other Hub", IsActive = true, HubId = env.Hub2, CreatedAt = Day },
            new User { Identifier = "SHIP-001", PasswordHash = "x", Role = Roles.Shipper, FullName = "A Shipper", IsActive = true, HubId = env.Hub1, CreatedAt = Day });
        env.Db.SaveChanges();

        HubStaffInfo staff = Assert.Single(env.Hubs.GetHub(env.Hub1).Value!.Staff);

        Assert.Equal("Tran Thi Mai", staff.FullName);
        Assert.Equal("STAFF-001", staff.Identifier);
    }

    // ---------- soft delete ----------

    [Fact]
    public void Deactivating_a_hub_keeps_the_row_and_its_old_orders_and_removes_it_from_the_active_hub_list()
    {
        using var env = new MenuTestEnv();
        env.AddOrder(env.Hub2, OrderStatuses.Delivered, Day);      // finished orders do not block
        env.AddOrder(env.Hub2, OrderStatuses.Cancelled, Day);

        Assert.True(env.Hubs.DeactivateHub(env.Hub2).Success);

        env.Db.ChangeTracker.Clear();
        Assert.False(env.Db.Hubs.Single(h => h.HubId == env.Hub2).IsActive);   // row kept
        Assert.Equal(2, env.Db.Orders.Count(o => o.HubId == env.Hub2));        // orders kept
        Assert.DoesNotContain(new HubRepository(env.Db).GetActiveHubs(), h => h.HubId == env.Hub2);
        Assert.False(env.Hubs.ListHubs().Single(h => h.HubId == env.Hub2).IsActive);   // still listed on the Admin page, as inactive
    }

    [Theory]
    [InlineData("Placed")]
    [InlineData("Confirmed")]
    [InlineData("Preparing")]
    [InlineData("Ready")]
    [InlineData("Out for Delivery")]
    public void A_hub_with_an_order_in_progress_cannot_be_deactivated(string status)
    {
        using var env = new MenuTestEnv();
        env.AddOrder(env.Hub1, status, Day);

        var result = env.Hubs.DeactivateHub(env.Hub1);

        Assert.False(result.Success);
        Assert.True(env.Db.Hubs.Single(h => h.HubId == env.Hub1).IsActive);
        // the edit form is refused in the same way
        Assert.False(env.Hubs.UpdateHub(env.Hub1, new HubChanges { Name = "Hub 1", Address = "a", KitchenCapacity = 10, IsActive = false }).Success);
    }

    [Fact]
    public void A_hub_can_be_deactivated_twice_only_once_and_activated_again_in_the_form()
    {
        using var env = new MenuTestEnv();
        Assert.True(env.Hubs.DeactivateHub(env.Hub2).Success);
        Assert.False(env.Hubs.DeactivateHub(env.Hub2).Success);

        var reactivate = env.Hubs.UpdateHub(env.Hub2, new HubChanges { Name = "Hub 2", Address = "b", KitchenCapacity = 20, IsActive = true });

        Assert.True(reactivate.Success);
        Assert.True(env.Hubs.GetHub(env.Hub2).Value!.IsActive);
    }
}
