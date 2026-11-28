using SaigonBite.Helpers;
using SaigonBite.Services;

namespace SaigonBite.Tests.Menu;

/// <summary>Hub Utilization Report R2 (FR16, test T-51). Real ReportService and HubRepository on EF InMemory.</summary>
public class HubUtilizationTests
{
    private static readonly DateOnly FirstDay = new(2026, 11, 1);
    private static readonly DateOnly LastDay = new(2026, 11, 13);
    private static readonly DateTime InRange = new(2026, 11, 10, 12, 0, 0);

    private static HubUtilizationData Run(MenuTestEnv env, DateOnly from, DateOnly to)
    {
        var result = env.Reports.HubUtilizationReport(from, to);
        Assert.True(result.Ok, string.Join(" | ", result.Errors));
        return result.Value!;
    }

    private static HubUtilizationRow Row(HubUtilizationData data, string hubName) => data.Hubs.Single(h => h.HubName == hubName);

    // Delivered order whose kitchen time (Confirmed to Ready) is the given number of minutes.
    private static void AddDelivered(MenuTestEnv env, int hubId, int prepMinutes, decimal quiet = 0, DateTime? created = null)
    {
        DateTime confirmed = (created ?? InRange).AddMinutes(1);
        env.AddOrder(hubId, OrderStatuses.Delivered, created ?? InRange, quiet, confirmed, confirmed.AddMinutes(prepMinutes));
    }

    // ---------- T-51 ----------

    [Fact]
    public void T51_matches_the_seed_data_and_an_empty_hub_shows_zeros()
    {
        using var env = new MenuTestEnv();   // Hub 1 has capacity 10, Hub 2 has no orders

        // 4 Delivered orders with known Confirmed-to-Ready times: 10, 12, 14 and 16 minutes, one with a Quiet Kitchen discount.
        AddDelivered(env, env.Hub1, 10, quiet: 5000);
        AddDelivered(env, env.Hub1, 12);
        AddDelivered(env, env.Hub1, 14);
        AddDelivered(env, env.Hub1, 16);
        // 2 orders still in Preparing (they are the load).
        env.AddOrder(env.Hub1, OrderStatuses.Preparing, InRange, confirmedAt: InRange.AddMinutes(1));
        env.AddOrder(env.Hub1, OrderStatuses.Preparing, InRange, confirmedAt: InRange.AddMinutes(2));

        HubUtilizationData data = Run(env, FirstDay, LastDay);

        HubUtilizationRow hub1 = Row(data, "Hub 1");
        Assert.Equal(6, hub1.OrdersHandled);          // 4 Delivered + 2 Preparing
        Assert.Equal(13.0m, hub1.AvgPrepMinutes);     // (10 + 12 + 14 + 16) / 4
        Assert.Equal(20m, hub1.CurrentLoadPct);       // 2 Preparing / capacity 10
        Assert.Equal(1, hub1.QuietDiscounts);

        HubUtilizationRow hub2 = Row(data, "Hub 2");  // the empty hub shows 0
        Assert.Equal(0, hub2.OrdersHandled);
        Assert.Equal(0m, hub2.AvgPrepMinutes);
        Assert.Equal(0m, hub2.CurrentLoadPct);
        Assert.Equal(0, hub2.QuietDiscounts);

        Assert.Equal(6, data.TotalOrdersHandled);
        Assert.Equal(1, data.TotalQuietDiscounts);
        Assert.Equal(2, data.ActiveHubCount);
    }

    // ---------- Rules of the figures ----------

    [Fact]
    public void Placed_and_Cancelled_orders_are_not_handled_and_get_no_discount_count()
    {
        using var env = new MenuTestEnv();
        env.AddOrder(env.Hub1, OrderStatuses.Placed, InRange, quietDiscount: 1000);
        env.AddOrder(env.Hub1, OrderStatuses.Cancelled, InRange, quietDiscount: 1000);
        env.AddOrder(env.Hub1, OrderStatuses.Failed, InRange, quietDiscount: 1000, confirmedAt: InRange);

        HubUtilizationRow row = Row(Run(env, FirstDay, LastDay), "Hub 1");

        Assert.Equal(1, row.OrdersHandled);   // only the Failed one: the kitchen took it on
        Assert.Equal(1, row.QuietDiscounts);
    }

    [Fact]
    public void Load_counts_only_Confirmed_and_Preparing_and_does_not_depend_on_the_date_range()
    {
        using var env = new MenuTestEnv();
        // Old orders (outside the range) that are still Confirmed or Preparing now.
        env.AddOrder(env.Hub1, OrderStatuses.Confirmed, new DateTime(2026, 10, 1, 9, 0, 0));
        env.AddOrder(env.Hub1, OrderStatuses.Preparing, new DateTime(2026, 10, 1, 9, 0, 0));
        env.AddOrder(env.Hub1, OrderStatuses.Ready, InRange);            // Ready is not load
        env.AddOrder(env.Hub1, OrderStatuses.Delivered, InRange);        // neither is Delivered

        HubUtilizationRow row = Row(Run(env, FirstDay, LastDay), "Hub 1");

        Assert.Equal(20m, row.CurrentLoadPct);   // 2 / 10
        Assert.Equal(2, row.OrdersHandled);      // Ready + Delivered are in the range; the old ones are not
    }

    [Fact]
    public void Only_orders_inside_the_range_count_and_the_last_day_is_included_to_its_end()
    {
        using var env = new MenuTestEnv();
        env.AddOrder(env.Hub1, OrderStatuses.Delivered, new DateTime(2026, 10, 31, 23, 59, 59));   // before
        env.AddOrder(env.Hub1, OrderStatuses.Delivered, new DateTime(2026, 11, 1, 0, 0, 0));       // first moment
        env.AddOrder(env.Hub1, OrderStatuses.Delivered, new DateTime(2026, 11, 13, 23, 59, 59));   // last moment
        env.AddOrder(env.Hub1, OrderStatuses.Delivered, new DateTime(2026, 11, 14, 0, 0, 0));      // after

        Assert.Equal(2, Row(Run(env, FirstDay, LastDay), "Hub 1").OrdersHandled);
    }

    [Fact]
    public void Orders_without_both_times_are_left_out_of_the_average()
    {
        using var env = new MenuTestEnv();
        AddDelivered(env, env.Hub1, 10);
        env.AddOrder(env.Hub1, OrderStatuses.Delivered, InRange);                                   // no history at all
        env.AddOrder(env.Hub1, OrderStatuses.Preparing, InRange, confirmedAt: InRange);             // not Ready yet

        HubUtilizationRow row = Row(Run(env, FirstDay, LastDay), "Hub 1");

        Assert.Equal(3, row.OrdersHandled);
        Assert.Equal(10.0m, row.AvgPrepMinutes);
    }

    [Fact]
    public void Average_prep_time_is_rounded_to_one_decimal()
    {
        using var env = new MenuTestEnv();
        AddDelivered(env, env.Hub1, 10);
        AddDelivered(env, env.Hub1, 10);
        AddDelivered(env, env.Hub1, 20);   // (10 + 10 + 20) / 3 = 13.333...

        Assert.Equal(13.3m, Row(Run(env, FirstDay, LastDay), "Hub 1").AvgPrepMinutes);
    }

    [Fact]
    public void Each_hub_only_counts_its_own_orders_and_inactive_hubs_are_not_listed()
    {
        using var env = new MenuTestEnv();
        AddDelivered(env, env.Hub1, 10);
        AddDelivered(env, env.Hub2, 20);
        AddDelivered(env, env.Hub2, 20);

        var inactive = new SaigonBite.Models.Entities.Hub { Name = "Closed Hub", Address = "c", KitchenCapacity = 10, IsActive = false };
        env.Db.Hubs.Add(inactive);
        env.Db.SaveChanges();
        AddDelivered(env, inactive.HubId, 5);

        HubUtilizationData data = Run(env, FirstDay, LastDay);

        Assert.Equal(2, data.Hubs.Count);
        Assert.Equal(1, Row(data, "Hub 1").OrdersHandled);
        Assert.Equal(2, Row(data, "Hub 2").OrdersHandled);
        Assert.Equal(new[] { "Hub 2", "Hub 1" }, data.Hubs.Select(h => h.HubName).ToArray());   // most orders first
        Assert.Equal(3, data.TotalOrdersHandled);
    }

    [Fact]
    public void Load_uses_each_hubs_own_capacity_and_a_full_hub_shows_100_percent()
    {
        using var env = new MenuTestEnv();   // Hub 1 capacity 10, Hub 2 capacity 20
        for (int i = 0; i < 9; i++) env.AddOrder(env.Hub1, OrderStatuses.Confirmed, InRange);      // 90%
        for (int i = 0; i < 5; i++) env.AddOrder(env.Hub2, OrderStatuses.Preparing, InRange);      // 25%

        HubUtilizationData data = Run(env, FirstDay, LastDay);

        Assert.Equal(90m, Row(data, "Hub 1").CurrentLoadPct);
        Assert.Equal(25m, Row(data, "Hub 2").CurrentLoadPct);
    }

    [Fact]
    public void An_end_date_before_the_start_date_fails_and_a_one_day_range_works()
    {
        using var env = new MenuTestEnv();

        var bad = env.Reports.HubUtilizationReport(LastDay, FirstDay);
        Assert.False(bad.Ok);
        Assert.NotEmpty(bad.Errors);

        Assert.True(env.Reports.HubUtilizationReport(FirstDay, FirstDay).Ok);
    }
}
