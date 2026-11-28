using Microsoft.EntityFrameworkCore;
using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Repositories;
using SaigonBite.Services;
using SaigonBite.Validators;

namespace SaigonBite.Tests.Menu;

/// <summary>
/// UC3 tests: the REAL validator, repositories and services on an EF InMemory database.
/// One hub "Hub 1" (capacity 10) that operates the brand "Pho 24".
/// </summary>
public sealed class MenuTestEnv : IDisposable
{
    public SaigonBiteContext Db { get; }
    public MenuService Menu { get; }
    public ReportService Reports { get; }
    public HubService Hubs { get; }
    public OrderCatalogRepository Catalog { get; }

    public int Hub1 { get; }
    public int Hub2 { get; }

    public MenuTestEnv()
    {
        var options = new DbContextOptionsBuilder<SaigonBiteContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Db = new SaigonBiteContext(options);

        var hub1 = new Hub { Name = "Hub 1", Address = "a", KitchenCapacity = 10, IsActive = true };
        var hub2 = new Hub { Name = "Hub 2", Address = "b", KitchenCapacity = 20, IsActive = true };
        Db.Hubs.AddRange(hub1, hub2);
        Db.SaveChanges();
        Hub1 = hub1.HubId;
        Hub2 = hub2.HubId;

        Menu = new MenuService(new BrandRepository(Db), new MenuItemRepository(Db), new HubRepository(Db), new MenuValidator());
        Reports = new ReportService(new OrderRepository(Db), new HubRepository(Db));
        Hubs = new HubService(new HubRepository(Db), new BrandRepository(Db), new HubValidator());
        Catalog = new OrderCatalogRepository(Db);
    }

    public void Dispose() => Db.Dispose();

    /// <summary>A brand created through the service. It is operated by Hub 1 when atHub1 is true.</summary>
    public int AddBrand(string name, bool atHub1 = true, bool active = true)
    {
        var result = Menu.CreateBrand(new BrandChanges { Name = name, IsActive = active });
        Assert.True(result.Ok, string.Join(" | ", result.Errors));

        if (atHub1)
        {
            // Which hub operates a brand is set in the hub editor (not UC3), so the test links it directly.
            Hub hub = Db.Hubs.Include(h => h.Brands).Single(h => h.HubId == Hub1);
            hub.Brands.Add(Db.Brands.Single(b => b.BrandId == result.Value));
            Db.SaveChanges();
        }
        return result.Value;
    }

    public static MenuItemChanges Item(int brandId, string name = "Pho Bo", string category = "Noodles",
                                       decimal? price = 50000, bool available = true) =>
        new() { BrandId = brandId, Name = name, Category = category, Price = price, IsAvailable = available };

    public int AddItem(int brandId, string name = "Pho Bo", string category = "Noodles", decimal price = 50000, bool available = true)
    {
        var result = Menu.CreateItem(Item(brandId, name, category, price, available));
        Assert.True(result.Ok, string.Join(" | ", result.Errors));
        return result.Value;
    }

    /// <summary>An order of Hub 1 with the given status. Statuses are given as history rows when times are passed.</summary>
    public Order AddOrder(int hubId, string status, DateTime createdAt, decimal quietDiscount = 0,
                          DateTime? confirmedAt = null, DateTime? readyAt = null, (int ItemId, int Quantity, decimal UnitPrice)[]? lines = null)
    {
        lines ??= Array.Empty<(int ItemId, int Quantity, decimal UnitPrice)>();
        var order = new Order
        {
            CustomerId = 1,
            HubId = hubId,
            Status = status,
            DeliveryAddress = "12 Nguyen Hue",
            SubtotalS = lines.Sum(l => l.Quantity * l.UnitPrice),
            QuietDiscountQ = quietDiscount,
            Total = lines.Sum(l => l.Quantity * l.UnitPrice),
            CreatedAt = createdAt
        };
        foreach (var l in lines)
            order.OrderLines.Add(new OrderLine { ItemId = l.ItemId, Quantity = l.Quantity, UnitPrice = l.UnitPrice });
        if (confirmedAt.HasValue)
            order.OrderStatusHistories.Add(new OrderStatusHistory { FromStatus = OrderStatuses.Placed, ToStatus = OrderStatuses.Confirmed, ChangedAt = confirmedAt.Value, ChangedBy = 1 });
        if (readyAt.HasValue)
            order.OrderStatusHistories.Add(new OrderStatusHistory { FromStatus = OrderStatuses.Preparing, ToStatus = OrderStatuses.Ready, ChangedAt = readyAt.Value, ChangedBy = 1 });

        Db.Orders.Add(order);
        Db.SaveChanges();
        return order;
    }
}
