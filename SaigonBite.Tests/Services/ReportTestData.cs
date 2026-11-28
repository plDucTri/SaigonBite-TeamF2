using SaigonBite.Models.Entities;
using SaigonBite.Repositories;
using SaigonBite.Services;

namespace SaigonBite.Tests.Services;

/// <summary>Small helpers that put menu items and orders into the InMemory database for the report tests.</summary>
public static class ReportTestData
{
    // Real service and real repository, on the same InMemory database as the rest of the set-up.
    public static ReportService NewReport(RegistrationTestSetup setup) => new(new OrderRepository(setup.Db), new HubRepository(setup.Db));

    /// <summary>A menu item of its own brand, in the given category. active = false makes item and brand inactive.</summary>
    public static MenuItem AddItem(RegistrationTestSetup setup, string category, decimal price, bool active = true)
    {
        var item = new MenuItem
        {
            Brand = new Brand { Name = "Brand " + Guid.NewGuid().ToString("N"), IsActive = active },
            Name = category + " item",
            Category = category,
            Price = price,
            IsAvailable = true,
            IsActive = active
        };
        setup.Db.MenuItems.Add(item);
        setup.Db.SaveChanges();
        setup.Db.ChangeTracker.Clear();
        return item;
    }

    /// <summary>
    /// An order of the given customer with one payment and its lines (UnitPrice = the item price).
    /// The Total is given by the test, because the pricing engine belongs to UC1.
    /// </summary>
    public static Order AddOrder(
        RegistrationTestSetup setup,
        string customerIdentifier,
        string status,
        string paymentMethod,
        DateTime createdAt,
        decimal total,
        params (MenuItem Item, int Quantity)[] lines)
    {
        var order = new Order
        {
            CustomerId = setup.Db.Users.Single(u => u.Identifier == customerIdentifier).UserId,
            HubId = setup.Db.Hubs.First().HubId,
            Status = status,
            DeliveryAddress = "12 Nguyen Hue",
            SubtotalS = lines.Sum(l => l.Item.Price * l.Quantity),
            Total = total,
            CreatedAt = createdAt,
            Payment = new Payment { Method = paymentMethod, Amount = total, Status = "Paid" }
        };

        foreach (var (item, quantity) in lines)
            order.OrderLines.Add(new OrderLine { ItemId = item.ItemId, Quantity = quantity, UnitPrice = item.Price });

        setup.Db.Orders.Add(order);
        setup.Db.SaveChanges();
        setup.Db.ChangeTracker.Clear();
        return order;
    }
}
