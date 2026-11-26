using SaigonBite.Models.Entities;
using SaigonBite.Services;

namespace SaigonBite.Tests.Orders;

/// <summary>
/// Workflow tests of OrderService on an in-memory database. Test IDs T-xx are the IDs of the Test Cases sheet.
/// Rules that need no database are tested in PricingEngineTests, OrderValidatorTests, PaymentTests and
/// OrderStateMachineTests.
/// </summary>
public class OrderServiceTests
{
    // ================================================================ place an order (FR1 to FR7)

    // ---- T-18 (FR2, FR4, FR6, FR7) specification worked example, stored completely ----
    [Fact]
    public async Task T18_WorkedExample_IsPlacedAndStoredCompletely()
    {
        var env = OrderTestEnv.Create();
        env.AddHubOrders(env.Hub1, OrderStatus.Confirmed, 2);   // 2 of 10 = 20% load
        var request = env.Req(env.Hub1, "COD", 3.2m, new LineRequest(env.PhoBo, 2), new LineRequest(env.PhoGa, 1));   // S 200,000

        int id = await env.PlaceOk(env.LocalId, request);

        var order = env.Get(id);
        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Equal(200_000m, order.SubtotalS);
        Assert.Equal(20_000m, order.QuietDiscountQ);
        Assert.Equal(25_000m, order.DeliveryFeeD);
        Assert.Equal(205_000m, order.Total);
        Assert.Equal(2, env.Db.OrderLines.Count(l => l.OrderId == id));

        var snap = env.Db.PriceSnapshots.Single(s => s.OrderId == id);
        Assert.Equal(20m, snap.HubLoadPct);
        Assert.False(snap.HeavyRain);

        var pay = env.Db.Payments.Single(p => p.OrderId == id);
        Assert.Equal("COD", pay.Method);
        Assert.Equal(PaymentStatus.Pending, pay.Status);
        Assert.Equal(205_000m, pay.Amount);

        var history = env.Db.OrderStatusHistories.Single(h => h.OrderId == id);
        Assert.Null(history.FromStatus);
        Assert.Equal(OrderStatus.Placed, history.ToStatus);
        Assert.Equal(env.LocalId, history.ChangedBy);
    }

    // ---- T-01 (FR1, BR1) a rejected order saves nothing ----
    [Fact]
    public async Task T01_SubtotalBelowMinimum_IsRejected_AndNothingIsSaved()
    {
        var env = OrderTestEnv.Create();
        var outcome = await env.Service.PlaceOrderAsync(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.TraDao, 1)));

        Assert.False(outcome.Success);
        Assert.Contains(outcome.Violations, v => v.Rule == "BR1");
        Assert.Equal(0, env.Db.Orders.Count());
        Assert.Equal(0, env.Db.Payments.Count());
    }

    // ---- T-02 (FR1, BR2) ----
    [Theory]
    [InlineData(7.99, true)]
    [InlineData(8.0, true)]
    [InlineData(8.01, false)]
    public async Task T02_Coverage(double km, bool accepted)
    {
        var env = OrderTestEnv.Create();
        var outcome = await env.Service.PlaceOrderAsync(env.LocalId, env.Std().Also(r => r.DistanceKm = (decimal)km));
        Assert.Equal(accepted, outcome.Success);
        if (accepted) Assert.Equal(151_000m, env.Get(outcome.OrderId).Total);
        else Assert.Contains(outcome.Violations, v => v.Rule == "BR2");
    }

    // ---- T-12 (FR4, BR8) hub at capacity 100 with 89, 90 and 91 orders in Confirmed or Preparing ----
    [Theory]
    [InlineData(89, true)]
    [InlineData(90, false)]
    [InlineData(91, false)]
    public async Task T12_HubLoadProtection(int activeOrders, bool accepted)
    {
        var env = OrderTestEnv.Create();
        env.AddHubOrders(env.Hub2, OrderStatus.Confirmed, activeOrders);
        int before = env.Db.Orders.Count();

        var outcome = await env.Service.PlaceOrderAsync(env.LocalId, env.Req(env.Hub2, "MoMo", 1m, new LineRequest(env.StdDish, 1)));

        Assert.Equal(accepted, outcome.Success);
        Assert.Equal(!accepted, outcome.HubTooBusy);
        Assert.Equal(accepted ? before + 1 : before, env.Db.Orders.Count());
        if (accepted) Assert.Equal(115_000m, env.Get(outcome.OrderId).Total);
    }

    [Fact]
    public async Task HubLoad_CountsOnlyConfirmedAndPreparing()
    {
        var env = OrderTestEnv.Create();
        env.AddHubOrders(env.Hub1, OrderStatus.Placed, 3);
        env.AddHubOrders(env.Hub1, OrderStatus.Ready, 3);
        env.AddHubOrders(env.Hub1, OrderStatus.Confirmed, 1);
        env.AddHubOrders(env.Hub1, OrderStatus.Preparing, 1);
        Assert.Equal(20m, await env.Service.GetHubLoadPctAsync(env.Hub1));   // 2 of 10
    }

    // ---- T-21 (FR1, BR12) quantity 0 and 21 rejected; 1 and 20 accepted ----
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public async Task T21_QuantityPerLine(int quantity, bool accepted)
    {
        var env = OrderTestEnv.Create();
        var item = new MenuItem { BrandId = env.Db.Brands.First().BrandId, Name = "Qty item", Category = "c", Price = quantity == 1 ? 50_000m : 5_000m, IsAvailable = true, IsActive = true };
        env.Db.MenuItems.Add(item);
        env.Db.SaveChanges();
        env.AddHubOrders(env.Hub2, OrderStatus.Confirmed, 50);

        var outcome = await env.Service.PlaceOrderAsync(env.LocalId, env.Req(env.Hub2, "MoMo", 1m, new LineRequest(item.ItemId, quantity)));

        Assert.Equal(accepted, outcome.Success);
        if (quantity == 1) Assert.Equal(65_000m, env.Get(outcome.OrderId).Total);
        if (quantity == 20) Assert.Equal(115_000m, env.Get(outcome.OrderId).Total);   // S 100,000
    }

    // ---- T-22 (FR1, BR12) 30 lines accepted, 31 rejected (items of 2,000) ----
    [Theory]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public async Task T22_LinesPerOrder(int lineCount, bool accepted)
    {
        var env = OrderTestEnv.Create();
        int brandId = env.Db.Brands.First().BrandId;
        var items = Enumerable.Range(1, lineCount)
            .Select(i => new MenuItem { BrandId = brandId, Name = "Cheap " + i, Category = "c", Price = 2_000m, IsAvailable = true, IsActive = true })
            .ToList();
        env.Db.MenuItems.AddRange(items);
        env.Db.SaveChanges();
        env.AddHubOrders(env.Hub2, OrderStatus.Confirmed, 50);

        var outcome = await env.Service.PlaceOrderAsync(env.LocalId,
            env.Req(env.Hub2, "MoMo", 1m, items.Select(i => new LineRequest(i.ItemId, 1)).ToArray()));

        Assert.Equal(accepted, outcome.Success);
        if (accepted) Assert.Equal(75_000m, env.Get(outcome.OrderId).Total);   // S 60,000 + D 15,000
    }

    [Fact]
    public async Task SameItemTwice_IsMerged_AndTheMergedQuantityIsChecked()
    {
        var env = OrderTestEnv.Create();
        var merged = await env.Service.PlaceOrderAsync(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 12), new LineRequest(env.PhoBo, 12)));
        Assert.Contains(merged.Violations, v => v.Rule == "BR12");   // 12 + 12 = 24 is over 20
    }

    // ---- T-23, T-24 (FR1, BR12) ----
    [Fact]
    public async Task T23_T24_UnavailableAndForeignBrandItems_AreRejected()
    {
        var env = OrderTestEnv.Create();
        var a = await env.Service.PlaceOrderAsync(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 1), new LineRequest(env.Unavailable, 1)));
        Assert.Contains(a.Violations, v => v.Message.Contains("unavailable"));

        var b = await env.Service.PlaceOrderAsync(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 1), new LineRequest(env.OtherBrandItem, 1)));
        Assert.Contains(b.Violations, v => v.Message.Contains("not sold by this hub"));
        Assert.Equal(0, env.Db.Orders.Count());
    }

    // ---- T-25, T-26 (FR7, BR13) the wrong customer type creates no order ----
    [Theory]
    [InlineData("Local", "PayPal")]
    [InlineData("Local", "Apple Pay")]
    [InlineData("Tourist", "MoMo")]
    [InlineData("Tourist", "VNPay")]
    public async Task T25_T26_PaymentMethodOfTheOtherCustomerType_IsRejected(string role, string method)
    {
        var env = OrderTestEnv.Create();
        int userId = role == "Local" ? env.LocalId : env.TouristId;
        var outcome = await env.Service.PlaceOrderAsync(userId, env.Std(method: method));
        Assert.Contains(outcome.Violations, v => v.Rule == "BR13");
        Assert.Equal(50, env.Db.Orders.Count());   // only the 50 orders that set the hub load
    }

    [Theory]
    [InlineData("Local", "MoMo")]
    [InlineData("Local", "VNPay")]
    [InlineData("Local", "COD")]
    [InlineData("Tourist", "Apple Pay")]
    [InlineData("Tourist", "PayPal")]
    [InlineData("Tourist", "COD")]
    public async Task T25_T26_OwnPaymentMethods_AreAccepted(string role, string method)
    {
        var env = OrderTestEnv.Create();
        int userId = role == "Local" ? env.LocalId : env.TouristId;
        int id = await env.PlaceOk(userId, env.Std(method: method));
        Assert.Equal(method, env.Db.Payments.Single(p => p.OrderId == id).Method);
    }

    // ---- T-27 (FR7, BR14) COD limit: 20 x 49,950 / 20 x 50,000 / 20 x 50,050 ----
    [Theory]
    [InlineData(49_950, true)]    // S 999,000
    [InlineData(50_000, true)]    // S 1,000,000
    [InlineData(50_050, false)]   // S 1,001,000: COD refused, online payment still allowed
    public async Task T27_CodLimit(int price, bool codAccepted)
    {
        var env = OrderTestEnv.Create();
        var item = new MenuItem { BrandId = env.Db.Brands.First().BrandId, Name = "Big " + price, Category = "c", Price = price, IsAvailable = true, IsActive = true };
        env.Db.MenuItems.Add(item);
        env.Db.SaveChanges();
        env.AddHubOrders(env.Hub2, OrderStatus.Confirmed, 50);   // load 50%: no Quiet discount; S >= 300,000 so delivery is free

        var cod = await env.Service.PlaceOrderAsync(env.LocalId, env.Req(env.Hub2, "COD", 1m, new LineRequest(item.ItemId, 20)));
        Assert.Equal(codAccepted, cod.Success);
        if (!codAccepted)
        {
            Assert.Contains(cod.Violations, v => v.Rule == "BR14");
            int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub2, "MoMo", 1m, new LineRequest(item.ItemId, 20)));
            Assert.Equal(1_001_000m, env.Get(id).Total);
        }
    }

    // ---- T-17b (FR5, BR9) a second voucher is ignored; T-13 / T-16 a voucher that does not qualify is refused ----
    [Fact]
    public async Task T17b_OnlyOneVoucherIsApplied()
    {
        var env = OrderTestEnv.Create();
        var outcome = await env.Service.PlaceOrderAsync(env.LocalId, env.Std(voucher: "V10, FIX50"));

        Assert.True(outcome.Success);
        var order = env.Get(outcome.OrderId);
        Assert.Equal("V10", order.VoucherCode);
        Assert.Equal(10_000m, order.VoucherDiscountV);
        Assert.Equal(105_000m, order.Total);
        Assert.Contains(outcome.Warnings, w => w.Contains("Only one voucher"));
    }

    [Fact]
    public async Task T13_VoucherBelowMinimum_IsRefused_ButTheOrderIsPlaced()
    {
        var env = OrderTestEnv.Create();
        env.AddHubOrders(env.Hub2, OrderStatus.Confirmed, 50);
        var request = env.Req(env.Hub2, "MoMo", 1m, new LineRequest(env.PhoBo, 1), new LineRequest(env.TraDao, 1)).With("V10");   // S 95,000 < 100,000

        var outcome = await env.Service.PlaceOrderAsync(env.LocalId, request);

        Assert.True(outcome.Success);
        Assert.Contains(outcome.Warnings, w => w.Contains("V10"));
        var order = env.Get(outcome.OrderId);
        Assert.Null(order.VoucherCode);
        Assert.Equal(0m, order.VoucherDiscountV);
        Assert.Equal(110_000m, order.Total);   // 95,000 + 15,000
    }

    [Fact]
    public async Task UnknownVoucherCode_IsRefusedWithANote()
    {
        var env = OrderTestEnv.Create();
        var outcome = await env.Service.PlaceOrderAsync(env.LocalId, env.Std(voucher: "NOPE"));
        Assert.True(outcome.Success);
        Assert.Contains(outcome.Warnings, w => w.Contains("NOPE"));
        Assert.Equal(115_000m, env.Get(outcome.OrderId).Total);
    }

    // ---- T-56 (FR7, A-03) the mock gateway is set to fail: no order, line, payment or history row ----
    [Fact]
    public async Task T56_FailedOnlinePayment_CreatesNothing()
    {
        var env = OrderTestEnv.Create();
        var request = env.Std();
        int ordersBefore = env.Db.Orders.Count();
        env.Gateway.SetDeclineAll(true);

        var outcome = await env.Service.PlaceOrderAsync(env.LocalId, request);

        Assert.False(outcome.Success);
        Assert.True(outcome.PaymentFailed);
        Assert.Equal(ordersBefore, env.Db.Orders.Count());
        Assert.Equal(0, env.Db.OrderLines.Count());
        Assert.Equal(0, env.Db.Payments.Count());
        Assert.Equal(0, env.Db.OrderStatusHistories.Count());
        Assert.Equal(0, env.Db.PriceSnapshots.Count());

        // choosing another method works (COD does not use the gateway)
        env.Gateway.SetDeclineAll(false);
        await env.PlaceOk(env.LocalId, env.Std(method: "COD"));
    }

    // ---- T-40 (FR1, FR2, NFR1) the price calculation for 30 lines, 20 runs, each under 1.5 seconds ----
    [Fact]
    public async Task T40_PriceCalculation_Under1500ms_For30Lines()
    {
        var env = OrderTestEnv.Create();
        int brandId = env.Db.Brands.First().BrandId;
        var items = Enumerable.Range(1, 30)
            .Select(i => new MenuItem { BrandId = brandId, Name = "Speed " + i, Category = "c", Price = 65_000m, IsAvailable = true, IsActive = true })
            .ToList();
        env.Db.MenuItems.AddRange(items);
        env.Db.SaveChanges();
        var request = env.Req(env.Hub1, "MoMo", 7.5m, items.Select(i => new LineRequest(i.ItemId, 20)).ToArray());
        request.VoucherCode = "V10";

        double slowest = 0;
        for (int run = 0; run < 20; run++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var preview = await env.Service.PreviewAsync(env.LocalId, request);
            sw.Stop();
            Assert.NotNull(preview);
            slowest = Math.Max(slowest, sw.Elapsed.TotalMilliseconds);
        }
        Assert.True(slowest < 1500, $"slowest run took {slowest} ms");
    }

    [Fact]
    public async Task Preview_ShowsTheBreakdown_AndSavesNothing()
    {
        var env = OrderTestEnv.Create();
        var preview = await env.Service.PreviewAsync(env.LocalId, env.Req(env.Hub1, "", 3m, new LineRequest(env.Banquet, 1)));

        Assert.NotNull(preview);
        Assert.Equal(1_080_000m, preview!.Pricing.Total);   // 0% load: Q 120,000; S >= 300,000 so delivery is free
        Assert.False(preview.PaymentOptions.Single(o => o.Method == "COD").Enabled);   // Figma D18
        Assert.True(preview.PaymentOptions.Single(o => o.Method == "MoMo").Enabled);
        Assert.Empty(preview.Violations);
        Assert.Equal(0, env.Db.Orders.Count());
    }

    // ================================================================ edit while Placed (FR11, BR17)

    // ---- T-41 (FR11, NFR6) the edit is recalculated from the snapshot ----
    [Fact]
    public async Task T41_Edit_RecalculatesFromTheSnapshot_NotFromCurrentConditions()
    {
        var env = OrderTestEnv.Create();
        env.AddHubOrders(env.Hub2, OrderStatus.Confirmed, 20);   // load 20%
        env.Rain.Set(true);
        env.Clock.SetTime(12, 0);                                // rush hour
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub2, "MoMo", 1m, new LineRequest(env.StdDish, 1)));
        Assert.Equal(118_000m, env.Get(id).Total);               // 100,000 - Q 10,000 + D 15,000 + R 3,000 + R2 10,000

        // Conditions change after placement: rain off, after 13:00, load 50%
        env.Rain.Set(false);
        env.Clock.SetTime(13, 30);
        env.AddHubOrders(env.Hub2, OrderStatus.Confirmed, 30);

        var result = await env.Service.EditOrderAsync(id, env.LocalId, new[] { new LineRequest(env.StdDish, 2) });   // S 200,000

        Assert.True(result.Success, string.Join("; ", result.Errors));
        var order = env.Get(id);
        Assert.Equal(200_000m, order.SubtotalS);
        Assert.Equal(20_000m, order.QuietDiscountQ);   // the 20% load of the snapshot
        Assert.Equal(3_000m, order.RushSurchargeR);    // the 12:00 of the snapshot
        Assert.Equal(10_000m, order.RainSurchargeR2);  // the rain flag of the snapshot
        Assert.Equal(208_000m, order.Total);
        Assert.Equal(208_000m, env.Db.Payments.Single(p => p.OrderId == id).Amount);
        Assert.Equal(OrderStatus.Placed, order.Status);
    }

    // ---- T-42 (FR11, BR17) edit after Confirmed is refused ----
    [Fact]
    public async Task T42_Edit_AfterConfirmed_IsRejected()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 1)));
        Assert.True((await env.Service.ChangeStatusAsync(id, env.KitchenId, OrderStatus.Confirmed)).Success);

        var result = await env.Service.EditOrderAsync(id, env.LocalId, new[] { new LineRequest(env.PhoBo, 2) });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.StartsWith("BR17"));
        Assert.Equal(65_000m, env.Get(id).SubtotalS);
        Assert.Equal(1, env.Db.OrderLines.Count(l => l.OrderId == id));
    }

    [Fact]
    public async Task Edit_ExistingLinesKeepTheirPlacementPrice()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 1), new LineRequest(env.PhoGa, 1)));
        env.Db.MenuItems.Single(m => m.ItemId == env.PhoBo).Price = 99_000m;   // the menu price changes after the order
        env.Db.SaveChanges();

        var result = await env.Service.EditOrderAsync(id, env.LocalId, new[] { new LineRequest(env.PhoBo, 2), new LineRequest(env.PhoGa, 1) });

        Assert.True(result.Success);
        Assert.Equal(65_000m, env.Db.OrderLines.Single(l => l.OrderId == id && l.ItemId == env.PhoBo).UnitPrice);
        Assert.Equal(200_000m, env.Get(id).SubtotalS);
    }

    [Fact]
    public async Task Edit_CanAddAndRemoveItems()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 1), new LineRequest(env.PhoGa, 1)));

        var result = await env.Service.EditOrderAsync(id, env.LocalId, new[] { new LineRequest(env.PhoGa, 1), new LineRequest(env.TraDao, 2) });

        Assert.True(result.Success, string.Join("; ", result.Errors));
        var items = env.Db.OrderLines.Where(l => l.OrderId == id).Select(l => l.ItemId).OrderBy(x => x).ToList();
        Assert.Equal(new[] { env.PhoGa, env.TraDao }.OrderBy(x => x).ToList(), items);
    }

    [Fact]
    public async Task Edit_ThatBreaksTheMinimumSubtotal_LeavesTheOrderUnchanged()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 1), new LineRequest(env.PhoGa, 1)));
        decimal totalBefore = env.Get(id).Total;

        var result = await env.Service.EditOrderAsync(id, env.LocalId, new[] { new LineRequest(env.TraDao, 1) });   // 30,000

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.StartsWith("BR1"));
        Assert.Equal(totalBefore, env.Get(id).Total);
        Assert.Equal(2, env.Db.OrderLines.Count(l => l.OrderId == id));
    }

    [Fact]
    public async Task Edit_ByAnotherCustomer_IsRefused()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 1)));
        Assert.False((await env.Service.EditOrderAsync(id, env.TouristId, new[] { new LineRequest(env.PhoBo, 5) })).Success);
    }

    [Fact]
    public async Task Edit_IsNotRefused_WhenTheHubIsNowBusy_A10()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 1), new LineRequest(env.PhoGa, 1)));
        env.AddHubOrders(env.Hub1, OrderStatus.Confirmed, 9);   // the hub is now at 90%

        Assert.True((await env.Service.EditOrderAsync(id, env.LocalId, new[] { new LineRequest(env.PhoBo, 2), new LineRequest(env.PhoGa, 1) })).Success);
    }

    // ================================================================ lifecycle (FR8, FR10, NFR5)

    // ---- T-32 (FR8, FR9) valid lifecycle from Placed to Delivered, COD ----
    [Fact]
    public async Task T32_FullCodFlow_DeliveredOnlyAfterCashConfirmed()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "COD", 3.2m, new LineRequest(env.PhoBo, 2)));

        await env.KitchenToReady(id, env.KitchenId);
        Assert.True((await env.Service.AcceptJobAsync(id, env.ShipperId)).Success);
        Assert.True((await env.Service.ChangeStatusAsync(id, env.ShipperId, OrderStatus.OutForDelivery)).Success);
        Assert.True((await env.Service.ConfirmCashAsync(id, env.ShipperId)).Success);
        Assert.True((await env.Service.ChangeStatusAsync(id, env.ShipperId, OrderStatus.Delivered)).Success);

        Assert.Equal(OrderStatus.Delivered, env.Get(id).Status);
        Assert.Equal(PaymentStatus.CashCollected, env.Db.Payments.Single(p => p.OrderId == id).Status);

        // NFR5: one history row per status change, each with the acting user
        var rows = env.Db.OrderStatusHistories.Where(h => h.OrderId == id).OrderBy(h => h.HistoryId).ToList();
        Assert.Equal(new[] { "Placed", "Confirmed", "Preparing", "Ready", "Out for Delivery", "Delivered" }, rows.Select(h => h.ToStatus).ToArray());
        Assert.Equal(env.ShipperId, rows.Last().ChangedBy);
        Assert.Equal(env.KitchenId, rows[1].ChangedBy);
    }

    // ---- T-33 (FR8, FR10, BR16) customer cancels Placed (A) and Confirmed online order (B) ----
    [Fact]
    public async Task T33_CustomerCancels_PlacedOrder_AndConfirmedOnlineOrder_IsRefunded()
    {
        var env = OrderTestEnv.Create();
        int a = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "COD", 1m, new LineRequest(env.PhoBo, 2)));
        Assert.True((await env.Service.CancelAsync(a, env.LocalId)).Success);
        Assert.Equal(OrderStatus.Cancelled, env.Get(a).Status);

        int b = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));
        await env.Service.ChangeStatusAsync(b, env.KitchenId, OrderStatus.Confirmed);
        Assert.True((await env.Service.CancelAsync(b, env.LocalId)).Success);

        var pay = env.Db.Payments.Single(p => p.OrderId == b);
        Assert.True(pay.Refunded);
        Assert.Equal(PaymentStatus.Refunded, pay.Status);
        Assert.Equal(pay.Amount, env.Get(b).Total);   // refunded in full
    }

    // ---- T-34 (FR8) valid failed delivery ----
    [Fact]
    public async Task T34_FailedDelivery_BecomesFailed()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "COD", 1m, new LineRequest(env.PhoBo, 2)));
        await env.KitchenToReady(id, env.KitchenId);
        await env.Service.AcceptJobAsync(id, env.ShipperId);
        await env.Service.ChangeStatusAsync(id, env.ShipperId, OrderStatus.OutForDelivery);

        Assert.True((await env.Service.ChangeStatusAsync(id, env.ShipperId, OrderStatus.Failed, "Customer unreachable")).Success);

        Assert.Equal(OrderStatus.Failed, env.Get(id).Status);
        Assert.Equal("Customer unreachable", env.Db.OrderStatusHistories.Single(h => h.OrderId == id && h.ToStatus == OrderStatus.Failed).Reason);
    }

    // ---- T-55 (FR8, FR10, A-02) failed delivery: online refunded in full, COD has nothing to refund ----
    [Fact]
    public async Task T55_FailedDelivery_RefundsOnlinePayment_CodHasNothingToRefund()
    {
        var env = OrderTestEnv.Create();
        int online = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));
        int cod = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "COD", 1m, new LineRequest(env.PhoBo, 2)));

        foreach (var id in new[] { online, cod })
        {
            await env.KitchenToReady(id, env.KitchenId);
            await env.Service.AcceptJobAsync(id, env.ShipperId);
            await env.Service.ChangeStatusAsync(id, env.ShipperId, OrderStatus.OutForDelivery);
            Assert.True((await env.Service.ChangeStatusAsync(id, env.ShipperId, OrderStatus.Failed, "Customer unreachable")).Success);
        }

        var onlinePay = env.Db.Payments.Single(p => p.OrderId == online);
        Assert.True(onlinePay.Refunded);
        Assert.Equal(PaymentStatus.Refunded, onlinePay.Status);

        var codPay = env.Db.Payments.Single(p => p.OrderId == cod);
        Assert.False(codPay.Refunded);
        Assert.Equal(PaymentStatus.Voided, codPay.Status);

        // a Failed order is terminal
        Assert.False((await env.Service.ChangeStatusAsync(online, env.ShipperId, OrderStatus.Delivered)).Success);   // T-64
    }

    // ---- T-37 (FR8, FR10, BR16) no cancellation after Preparing ----
    [Fact]
    public async Task T37_Cancel_AfterPreparing_IsRejected()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));
        await env.Service.ChangeStatusAsync(id, env.KitchenId, OrderStatus.Confirmed);
        await env.Service.ChangeStatusAsync(id, env.KitchenId, OrderStatus.Preparing);

        var result = await env.Service.CancelAsync(id, env.LocalId);

        Assert.False(result.Success);
        Assert.Equal(OrderStatus.Preparing, env.Get(id).Status);
        Assert.False(env.Db.Payments.Single(p => p.OrderId == id).Refunded);
    }

    // ---- T-35, T-36, T-38, T-39 (FR8) rejected by the business layer and the status stays ----
    [Fact]
    public async Task T35_T36_T38_T39_InvalidMoves_AreRejected_ByOrderService()
    {
        var env = OrderTestEnv.Create();
        int placed = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));

        // T-39 Placed to Ready by kitchen
        Assert.False((await env.Service.ChangeStatusAsync(placed, env.KitchenId, OrderStatus.Ready)).Success);
        Assert.Equal(OrderStatus.Placed, env.Get(placed).Status);
        Assert.Equal(1, env.Db.OrderStatusHistories.Count(h => h.OrderId == placed));   // no history row for a rejected move

        // T-36 Cancelled to Confirmed
        int cancelled = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));
        await env.Service.CancelAsync(cancelled, env.LocalId);
        Assert.False((await env.Service.ChangeStatusAsync(cancelled, env.KitchenId, OrderStatus.Confirmed)).Success);
        Assert.Equal(OrderStatus.Cancelled, env.Get(cancelled).Status);

        // T-38 Ready to Delivered, T-35 Delivered to Preparing
        int delivered = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));
        await env.KitchenToReady(delivered, env.KitchenId);
        await env.Service.AcceptJobAsync(delivered, env.ShipperId);
        Assert.False((await env.Service.ChangeStatusAsync(delivered, env.ShipperId, OrderStatus.Delivered)).Success);
        Assert.Equal(OrderStatus.Ready, env.Get(delivered).Status);

        await env.Service.ChangeStatusAsync(delivered, env.ShipperId, OrderStatus.OutForDelivery);
        Assert.True((await env.Service.ChangeStatusAsync(delivered, env.ShipperId, OrderStatus.Delivered)).Success);   // online: no cash step
        Assert.False((await env.Service.ChangeStatusAsync(delivered, env.KitchenId, OrderStatus.Preparing)).Success);
        Assert.Equal(OrderStatus.Delivered, env.Get(delivered).Status);
    }

    // ---- T-53 (FR8, A-01) the first shipper to accept is assigned ----
    [Fact]
    public async Task T53_FirstShipperToAccept_IsAssigned()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));
        await env.KitchenToReady(id, env.KitchenId);

        Assert.True((await env.Service.AcceptJobAsync(id, env.ShipperId)).Success);
        Assert.False((await env.Service.AcceptJobAsync(id, env.Shipper2Id)).Success);
        Assert.False((await env.Service.ChangeStatusAsync(id, env.Shipper2Id, OrderStatus.OutForDelivery)).Success);
        Assert.True((await env.Service.ChangeStatusAsync(id, env.ShipperId, OrderStatus.OutForDelivery)).Success);
        Assert.Equal(env.ShipperId, env.Get(id).ShipperId);
    }

    // ---- T-54 (FR8, A-01) nobody accepts for 5 minutes: fewest active deliveries, then longest waiting ----
    [Fact]
    public async Task T54_AutomaticAssignment_AfterFiveMinutes()
    {
        var env = OrderTestEnv.Create();
        var t0 = new DateTime(2026, 11, 24, 10, 0, 0);
        env.AddShipperOrder(env.ShipperId, OrderStatus.OutForDelivery, t0);                // X: 2 active
        env.AddShipperOrder(env.ShipperId, OrderStatus.OutForDelivery, t0.AddMinutes(1));
        env.AddShipperOrder(env.Shipper2Id, OrderStatus.OutForDelivery, t0.AddMinutes(30)); // Y: 1 active, busy recently
        env.AddShipperOrder(env.Shipper3Id, OrderStatus.OutForDelivery, t0.AddMinutes(5));  // Z: 1 active, waited longer than Y

        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));
        await env.KitchenToReady(id, env.KitchenId);

        // before 5 minutes: nothing happens
        env.Clock.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal(0, await env.Service.AssignOverdueJobsAsync());
        Assert.Null(env.Get(id).ShipperId);

        // after 5 minutes: Z is assigned
        env.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(1, await env.Service.AssignOverdueJobsAsync());
        Assert.Equal(env.Shipper3Id, env.Get(id).ShipperId);

        // an order that is already accepted is not touched again
        Assert.Equal(0, await env.Service.AssignOverdueJobsAsync());
    }

    // ---- T-60 (FR10, BR15, BR16) kitchen rejects a Placed online order with a reason ----
    [Fact]
    public async Task T60_KitchenRejectsPlacedOrder_WithReason_AndRefund()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));

        Assert.True((await env.Service.CancelAsync(id, env.KitchenId, "Out of ingredients")).Success);

        var row = env.Db.OrderStatusHistories.Single(h => h.OrderId == id && h.ToStatus == OrderStatus.Cancelled);
        Assert.Equal("Out of ingredients", row.Reason);
        Assert.Equal(env.KitchenId, row.ChangedBy);
        Assert.True(env.Db.Payments.Single(p => p.OrderId == id).Refunded);
    }

    // ---- T-62 (FR8, BR15) kitchen staff of another hub cannot confirm ----
    [Fact]
    public async Task T62_KitchenOfAnotherHub_CannotConfirm()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));
        var result = await env.Service.ChangeStatusAsync(id, env.Kitchen2Id, OrderStatus.Confirmed);   // kitchen of Hub3
        Assert.False(result.Success);
        Assert.Equal(OrderStatus.Placed, env.Get(id).Status);
    }

    // ---- T-61, T-63 wrong actor ----
    [Fact]
    public async Task T61_T63_WrongActor_IsRejected()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));
        Assert.False((await env.Service.ChangeStatusAsync(id, env.LocalId, OrderStatus.Confirmed)).Success);   // T-61 customer confirms
        Assert.Equal(OrderStatus.Placed, env.Get(id).Status);

        await env.Service.ChangeStatusAsync(id, env.KitchenId, OrderStatus.Confirmed);
        Assert.False((await env.Service.ChangeStatusAsync(id, env.KitchenId, OrderStatus.Cancelled, "reason")).Success);   // T-63
        Assert.Equal(OrderStatus.Confirmed, env.Get(id).Status);
    }

    // ---- T-65 customer cancels an Out for Delivery order ----
    [Fact]
    public async Task T65_CustomerCannotCancel_OutForDelivery()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));
        await env.KitchenToReady(id, env.KitchenId);
        await env.Service.AcceptJobAsync(id, env.ShipperId);
        await env.Service.ChangeStatusAsync(id, env.ShipperId, OrderStatus.OutForDelivery);

        Assert.False((await env.Service.CancelAsync(id, env.LocalId)).Success);
        Assert.Equal(OrderStatus.OutForDelivery, env.Get(id).Status);
    }

    // ---- T-66 (FR7, FR8, BR14) COD order Delivered before the cash is confirmed ----
    [Fact]
    public async Task T66_CodOrder_CannotBeDelivered_BeforeCashIsConfirmed()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "COD", 1m, new LineRequest(env.PhoBo, 2)));
        await env.KitchenToReady(id, env.KitchenId);
        await env.Service.AcceptJobAsync(id, env.ShipperId);
        await env.Service.ChangeStatusAsync(id, env.ShipperId, OrderStatus.OutForDelivery);

        var result = await env.Service.ChangeStatusAsync(id, env.ShipperId, OrderStatus.Delivered);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.StartsWith("BR14"));
        Assert.Equal(OrderStatus.OutForDelivery, env.Get(id).Status);
    }

    [Fact]
    public async Task ConfirmCash_OnlineOrder_IsRefused()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "VNPay", 1m, new LineRequest(env.PhoBo, 2)));
        await env.KitchenToReady(id, env.KitchenId);
        await env.Service.AcceptJobAsync(id, env.ShipperId);
        await env.Service.ChangeStatusAsync(id, env.ShipperId, OrderStatus.OutForDelivery);
        Assert.False((await env.Service.ConfirmCashAsync(id, env.ShipperId)).Success);
    }

    // ---- T-67 (FR8, BR15) Ready to Out for Delivery with no shipper assigned ----
    [Fact]
    public async Task T67_NoShipperAssigned_CannotGoOutForDelivery()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));
        await env.KitchenToReady(id, env.KitchenId);

        Assert.False((await env.Service.ChangeStatusAsync(id, env.ShipperId, OrderStatus.OutForDelivery)).Success);
        Assert.Equal(OrderStatus.Ready, env.Get(id).Status);
    }

    // ---- T-68 (FR8, FR10, BR16) kitchen rejects without a reason ----
    [Fact]
    public async Task T68_KitchenRejectWithoutReason_IsRejected()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));
        Assert.False((await env.Service.CancelAsync(id, env.KitchenId, "   ")).Success);
        Assert.False((await env.Service.CancelAsync(id, env.KitchenId, null)).Success);
        Assert.Equal(OrderStatus.Placed, env.Get(id).Status);
    }

    // ---- T-69 (FR8, FR10) admin cancels a Confirmed order paid online; Placed cannot be cancelled by Admin ----
    [Fact]
    public async Task T69_AdminCancelsConfirmedOrder_WithRefund()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));
        Assert.False((await env.Service.CancelAsync(id, env.AdminId, "x")).Success);   // still Placed

        await env.Service.ChangeStatusAsync(id, env.KitchenId, OrderStatus.Confirmed);
        Assert.True((await env.Service.CancelAsync(id, env.AdminId, "Cancelled by admin")).Success);

        Assert.True(env.Db.Payments.Single(p => p.OrderId == id).Refunded);
        Assert.Equal(env.AdminId, env.Db.OrderStatusHistories.Single(h => h.OrderId == id && h.ToStatus == OrderStatus.Cancelled).ChangedBy);
    }

    // ================================================================ track (FR9, NFR4, NFR5)

    // ---- T-52 (FR9, NFR5) a customer sees only their own orders ----
    [Fact]
    public async Task T52_Customer_SeesOnlyOwnOrders_AndStatusHistory()
    {
        var env = OrderTestEnv.Create();
        int a = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));
        int b = await env.PlaceOk(env.TouristId, env.Req(env.Hub1, "PayPal", 1m, new LineRequest(env.PhoBo, 2)));

        var listA = await env.Service.TrackAsync(env.LocalId, null, null, null);
        Assert.Equal(new[] { a }, listA.Select(o => o.OrderId).ToArray());

        Assert.NotNull(await env.Service.GetOrderAsync(a, env.LocalId));
        Assert.Null(await env.Service.GetOrderAsync(b, env.LocalId));   // customer B's order is refused

        var detail = await env.Service.GetOrderAsync(a, env.LocalId);
        Assert.Single(detail!.OrderStatusHistories);
    }

    [Fact]
    public async Task OrderIsVisibleOnlyToTheRightUsers()
    {
        var env = OrderTestEnv.Create();
        int id = await env.PlaceOk(env.LocalId, env.Req(env.Hub1, "MoMo", 1m, new LineRequest(env.PhoBo, 2)));

        Assert.NotNull(await env.Service.GetOrderAsync(id, env.LocalId));      // owner
        Assert.NotNull(await env.Service.GetOrderAsync(id, env.KitchenId));    // kitchen of the hub
        Assert.NotNull(await env.Service.GetOrderAsync(id, env.AdminId));      // admin
        Assert.Null(await env.Service.GetOrderAsync(id, env.TouristId));       // another customer
        Assert.Null(await env.Service.GetOrderAsync(id, env.Kitchen2Id));      // kitchen of another hub
        Assert.Null(await env.Service.GetOrderAsync(id, env.ShipperId));       // not Ready and not assigned
    }
}

internal static class RequestTestExtensions
{
    /// <summary>Lets a test change one field of a request inside an expression.</summary>
    public static CheckoutRequest Also(this CheckoutRequest request, Action<CheckoutRequest> change)
    {
        change(request);
        return request;
    }
}
