using Microsoft.EntityFrameworkCore;
using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Services;

namespace SaigonBite.Tests.Services;

/// <summary>Whole-scenario tests for the workbook cases T-46 and T-47 (FR14). Single steps are in AccountAdminTests.</summary>
public class AdminAccountEvidenceTests
{
    private const string LocalPhone = "0901234567"; // seeded Local customer
    private const string Passport = "DEMO12345";    // seeded Foreign Tourist

    // T-46: Admin deactivates a customer who has past orders.
    [Fact]
    public void T46_deactivated_customer_cannot_sign_in_but_the_orders_stay_visible()
    {
        using var setup = AdminTestData.NewSetup();
        User customer = AdminTestData.Find(setup, LocalPhone);
        int adminId = AdminTestData.Find(setup, "ADMIN").UserId;

        // A past order with its payment and one status history row.
        var noodles = ReportTestData.AddItem(setup, "Noodles", 50000);
        Order past = ReportTestData.AddOrder(setup, LocalPhone, OrderStatuses.Delivered, PaymentMethods.MoMo,
            new DateTime(2026, 11, 10, 12, 0, 0), 115000, (noodles, 2));
        setup.Db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = past.OrderId,
            FromStatus = OrderStatuses.OutForDelivery,
            ToStatus = OrderStatuses.Delivered,
            ChangedAt = new DateTime(2026, 11, 10, 13, 0, 0),
            ChangedBy = adminId
        });
        setup.Db.SaveChanges();
        setup.Db.ChangeTracker.Clear();

        var deactivated = setup.Service.DeactivateAccount(customer.UserId, adminId);

        // Row kept with IsActive = false, and the sign-in is refused.
        Assert.True(deactivated.Ok);
        Assert.False(AdminTestData.Find(setup, LocalPhone).IsActive);
        Assert.False(setup.Service.Login(LocalPhone, DbSeeder.DemoPassword).Ok);

        // The order, its payment and its history can still be read.
        Order saved = setup.Db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Payment)
            .Include(o => o.OrderStatusHistories)
            .Single();
        Assert.Equal("Demo Local Customer", saved.Customer.FullName);
        Assert.Equal(115000m, saved.Total);
        Assert.Equal(PaymentMethods.MoMo, saved.Payment!.Method);
        Assert.Single(saved.OrderStatusHistories);
    }

    // T-47: Admin opens a tourist account, changes the name and hotel details, saves and reopens it.
    [Fact]
    public void T47_changes_are_shown_when_the_account_is_reopened()
    {
        using var setup = AdminTestData.NewSetup();
        int touristId = AdminTestData.Find(setup, Passport).UserId;
        AccountInfo before = setup.Service.GetAccount(touristId).Value!;

        var saved = setup.Service.UpdateAccount(touristId, new AccountChanges
        {
            FullName = "John Smith",
            Identifier = before.Identifier,
            Nationality = before.Nationality!,
            Email = before.Email!,
            HotelName = "Riverside Hotel",
            RoomNo = "502"
        });

        Assert.True(saved.Ok);
        AccountInfo reopened = setup.Service.GetAccount(touristId).Value!;
        Assert.Equal("John Smith", reopened.FullName);
        Assert.Equal("Riverside Hotel", reopened.HotelName);
        Assert.Equal("502", reopened.RoomNo);
        Assert.Equal(before.Identifier, reopened.Identifier); // the passport is unchanged
    }
}
