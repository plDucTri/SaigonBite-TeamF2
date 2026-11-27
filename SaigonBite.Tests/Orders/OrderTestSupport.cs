using Microsoft.EntityFrameworkCore;
using SaigonBite.Models.Entities;
using SaigonBite.Repositories;
using SaigonBite.Services;
using SaigonBite.Services.Payments;

namespace SaigonBite.Tests.Orders;

/// <summary>A clock the order tests set to an exact Vietnam local time (BR4 windows, the 5-minute rule of A-01).</summary>
public sealed class VietnamClock : TimeProvider
{
    private DateTime _utc = new DateTime(2026, 11, 24, 8, 0, 0, DateTimeKind.Utc);   // 15:00 in Vietnam

    public void Set(int year, int month, int day, int hour, int minute) =>
        _utc = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc).AddHours(-7);

    /// <summary>Sets the time of day on 24 November 2026 (inside the validity of voucher V10).</summary>
    public void SetTime(int hour, int minute) => Set(2026, 11, 24, hour, minute);

    public void Advance(TimeSpan time) => _utc += time;

    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(_utc, TimeSpan.Zero);
}

/// <summary>
/// An in-memory database with a small restaurant world and the REAL services wired as in Program.cs.
/// Hub1 capacity 10, Hub2 capacity 100 (the "Std" hub of the Test Cases sheet), Hub3 capacity 10 (another kitchen).
/// "Std" = Local Customer, hub load 50%, 1 km, 15:00, rain off, no voucher, online payment.
/// </summary>
public class OrderTestEnv
{
    public SaigonBiteContext Db = null!;
    public VietnamClock Clock = new();
    public HeavyRainFlag Rain = new();
    public GatewaySimulator Gateway = new();
    public OrderService Service = null!;

    public int Hub1, Hub2, Hub3;
    public int LocalId, TouristId, KitchenId, Kitchen2Id, ShipperId, Shipper2Id, Shipper3Id, AdminId;
    public int PhoBo, PhoGa, TraDao, StdDish, Unavailable, OtherBrandItem, Banquet;   // menu item ids

    public static OrderTestEnv Create()
    {
        var env = new OrderTestEnv();
        var options = new DbContextOptionsBuilder<SaigonBiteContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new SaigonBiteContext(options);
        env.Db = db;

        var brandA = new Brand { Name = "Pho 24", IsActive = true };
        var brandB = new Brand { Name = "Other Brand (not at any hub)", IsActive = true };
        db.Brands.AddRange(brandA, brandB);

        var h1 = new Hub { Name = "Hub 1", Address = "a", KitchenCapacity = 10, IsActive = true };
        var h2 = new Hub { Name = "Hub 2", Address = "b", KitchenCapacity = 100, IsActive = true };
        var h3 = new Hub { Name = "Hub 3", Address = "c", KitchenCapacity = 10, IsActive = true };
        h1.Brands.Add(brandA); h2.Brands.Add(brandA); h3.Brands.Add(brandA);
        db.Hubs.AddRange(h1, h2, h3);
        db.SaveChanges();

        MenuItem Item(Brand b, string name, decimal price) =>
            new MenuItem { Brand = b, Name = name, Category = "Noodles", Price = price, IsAvailable = true, IsActive = true };

        var phoBo = Item(brandA, "Pho Bo", 65_000m);
        var phoGa = Item(brandA, "Pho Ga", 70_000m);
        var traDao = Item(brandA, "Tra Dao", 30_000m);
        var stdDish = Item(brandA, "Std dish", 100_000m);
        var unavailable = Item(brandA, "Sold out dish", 40_000m);
        var other = Item(brandB, "Other brand dish", 50_000m);
        var banquet = Item(brandA, "Banquet tray", 1_200_000m);
        db.MenuItems.AddRange(phoBo, phoGa, traDao, stdDish, unavailable, other, banquet);

        User U(string id, string role, int? hubId = null) =>
            new User { Identifier = id, PasswordHash = "x", Role = role, FullName = id, IsActive = true, HubId = hubId, CreatedAt = DateTime.Now };

        var local = U("0901234567", "Local");
        var tourist = U("AB123456", "Tourist");
        var kitchen = U("kitchen1", "Kitchen", h1.HubId);
        var kitchen2 = U("kitchen3", "Kitchen", h3.HubId);
        var shipper = U("ship1", "Shipper");
        var shipper2 = U("ship2", "Shipper");
        var shipper3 = U("ship3", "Shipper");
        var admin = U("admin", "Admin");
        db.Users.AddRange(local, tourist, kitchen, kitchen2, shipper, shipper2, shipper3, admin);

        // The vouchers of seed script 03 (V10 valid 20/11 to 30/11; FIX50 until 31/12)
        db.Vouchers.Add(new Voucher { Code = "V10", Type = "PERCENT", Value = 10, MaxDiscount = 30_000m, MinSubtotal = 100_000m,
            ValidFrom = new DateOnly(2026, 11, 20), ValidTo = new DateOnly(2026, 11, 30), IsActive = true });
        db.Vouchers.Add(new Voucher { Code = "FIX50", Type = "FIXED", Value = 50_000m, MaxDiscount = null, MinSubtotal = 0m,
            ValidFrom = new DateOnly(2026, 11, 20), ValidTo = new DateOnly(2026, 12, 31), IsActive = true });
        db.SaveChanges();

        // IsAvailable has a database default of true in the scaffolded model, so false is set by an update
        unavailable.IsAvailable = false;
        db.SaveChanges();

        env.Hub1 = h1.HubId; env.Hub2 = h2.HubId; env.Hub3 = h3.HubId;
        env.LocalId = local.UserId; env.TouristId = tourist.UserId;
        env.KitchenId = kitchen.UserId; env.Kitchen2Id = kitchen2.UserId;
        env.ShipperId = shipper.UserId; env.Shipper2Id = shipper2.UserId; env.Shipper3Id = shipper3.UserId;
        env.AdminId = admin.UserId;
        env.PhoBo = phoBo.ItemId; env.PhoGa = phoGa.ItemId; env.TraDao = traDao.ItemId; env.StdDish = stdDish.ItemId;
        env.Unavailable = unavailable.ItemId; env.OtherBrandItem = other.ItemId; env.Banquet = banquet.ItemId;

        var payments = new PaymentService(new PaymentStrategyFactory(env.Gateway));
        env.Service = new OrderService(
            new OrderRepository(db), new OrderCatalogRepository(db), new VoucherRepository(db), new UserRepository(db),
            new OrderValidator(), new PricingEngine(), payments, env.Rain, env.Clock);
        return env;
    }

    /// <summary>Adds orders in the given status to a hub, to set its load (BR8). Hub2 has capacity 100, so 50 orders = 50%.</summary>
    public void AddHubOrders(int hubId, string status, int count)
    {
        for (int i = 0; i < count; i++)
            Db.Orders.Add(new Order
            {
                CustomerId = LocalId, HubId = hubId, Status = status, DistanceKm = 1, DeliveryAddress = "x",
                SubtotalS = 1, Total = 1, CreatedAt = DateTime.Now
            });
        Db.SaveChanges();
    }

    /// <summary>The Std order: Std dish (100,000) at Hub2 with 50 Confirmed orders already there (load 50%), 1 km, MoMo.</summary>
    public CheckoutRequest Std(int quantity = 1, string method = "MoMo", string? voucher = null)
    {
        if (Db.Orders.Count(o => o.HubId == Hub2 && o.Status == OrderStatus.Confirmed) == 0)
            AddHubOrders(Hub2, OrderStatus.Confirmed, 50);
        return Req(Hub2, method, 1m, new LineRequest(StdDish, quantity)).With(voucher);
    }

    public CheckoutRequest Req(int hubId, string method, decimal km, params LineRequest[] lines) => new CheckoutRequest
    {
        HubId = hubId,
        Lines = lines.ToList(),
        DistanceKm = km,
        DeliveryAddress = "12 Nguyen Hue, District 1",
        PaymentMethod = method
    };

    public async Task<int> PlaceOk(int userId, CheckoutRequest request)
    {
        var outcome = await Service.PlaceOrderAsync(userId, request);
        Assert.True(outcome.Success, string.Join("; ", outcome.Violations.Select(v => v.Rule + " " + v.Message)));
        return outcome.OrderId;
    }

    public Order Get(int orderId) => Db.Orders.Single(o => o.OrderId == orderId);

    /// <summary>Moves an order Placed, Confirmed, Preparing, Ready with the kitchen of Hub1.</summary>
    public async Task KitchenToReady(int orderId, int kitchenId)
    {
        foreach (var next in new[] { OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Ready })
            Assert.True((await Service.ChangeStatusAsync(orderId, kitchenId, next)).Success, next);
    }

    /// <summary>A shipper delivery already in progress (used to give shippers different workloads in T-54).</summary>
    public void AddShipperOrder(int shipperId, string status, DateTime lastActivity)
    {
        var order = new Order
        {
            CustomerId = LocalId, HubId = Hub1, ShipperId = shipperId, Status = status, DistanceKm = 1,
            DeliveryAddress = "x", SubtotalS = 1, Total = 1, CreatedAt = lastActivity
        };
        order.OrderStatusHistories.Add(new OrderStatusHistory { ToStatus = status, ChangedAt = lastActivity, ChangedBy = shipperId });
        Db.Orders.Add(order);
        Db.SaveChanges();
    }
}

public static class CheckoutRequestExtensions
{
    public static CheckoutRequest With(this CheckoutRequest request, string? voucher)
    {
        request.VoucherCode = voucher;
        return request;
    }
}
