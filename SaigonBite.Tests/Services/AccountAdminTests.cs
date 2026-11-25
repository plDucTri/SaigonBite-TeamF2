using Microsoft.EntityFrameworkCore;
using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Repositories;
using SaigonBite.Services;

namespace SaigonBite.Tests.Services;

public class AccountAdminTests
{
    private const string LocalPhone = "0901234567"; // the seeded Local customer

    private static User Find(RegistrationTestSetup setup, string identifier) => AdminTestData.Find(setup, identifier);

    private static int AdminId(RegistrationTestSetup setup) => Find(setup, "ADMIN").UserId;

    private static AccountChanges LocalChanges(string name = "Nguyen Van A", string phone = LocalPhone, string password = "") =>
        new() { FullName = name, Identifier = phone, NewPassword = password };

    // A second Local customer, to test "identifier already used".
    private static void AddLocal(RegistrationTestSetup setup, string phone)
    {
        setup.Db.Users.Add(new User
        {
            Identifier = phone,
            PasswordHash = setup.Hasher.Hash("password1"),
            Role = Roles.Local,
            FullName = "Other Person",
            IsActive = true,
            LocalProfile = new LocalProfile { PhoneNumber = phone }
        });
        setup.Db.SaveChanges();
        setup.Db.ChangeTracker.Clear();
    }

    // ---------- deactivate ----------

    [Fact]
    public void Deactivate_keeps_the_row_and_blocks_sign_in()
    {
        using var setup = AdminTestData.NewSetup();

        var result = setup.Service.DeactivateAccount(Find(setup, LocalPhone).UserId, AdminId(setup));

        Assert.True(result.Ok);
        Assert.False(Find(setup, LocalPhone).IsActive); // the row is still there, only the flag is off
        Assert.False(setup.Service.Login(LocalPhone, DbSeeder.DemoPassword).Ok);
    }

    [Fact]
    public void Deactivate_unknown_account_fails()
    {
        using var setup = AdminTestData.NewSetup();

        var result = setup.Service.DeactivateAccount(9999, AdminId(setup));

        Assert.False(result.Ok);
    }

    [Fact]
    public void Deactivate_twice_fails_the_second_time()
    {
        using var setup = AdminTestData.NewSetup();
        int id = Find(setup, LocalPhone).UserId;
        setup.Service.DeactivateAccount(id, AdminId(setup));

        var second = setup.Service.DeactivateAccount(id, AdminId(setup));

        Assert.False(second.Ok);
        Assert.Contains("already inactive", second.Errors[0]);
    }

    [Fact]
    public void Admin_cannot_deactivate_own_account()
    {
        using var setup = AdminTestData.NewSetup();
        int adminId = AdminId(setup);

        var result = setup.Service.DeactivateAccount(adminId, adminId);

        Assert.False(result.Ok);
        Assert.True(Find(setup, "ADMIN").IsActive);
    }

    [Fact]
    public void Orders_of_a_deactivated_account_can_still_be_read()
    {
        using var setup = AdminTestData.NewSetup();
        User customer = Find(setup, LocalPhone);
        setup.Db.Orders.Add(new Order
        {
            CustomerId = customer.UserId,
            HubId = setup.Db.Hubs.First().HubId,
            Status = "Delivered",
            DeliveryAddress = "12 Nguyen Hue",
            Total = 100000
        });
        setup.Db.SaveChanges();

        setup.Service.DeactivateAccount(customer.UserId, AdminId(setup));

        var orders = setup.Db.Orders.Include(o => o.Customer).ToList();
        Assert.Single(orders);
        Assert.False(orders[0].Customer.IsActive);
        Assert.Equal("Demo Local Customer", orders[0].Customer.FullName);
    }

    // ---------- edit ----------

    [Fact]
    public void Edit_saves_the_new_name()
    {
        using var setup = AdminTestData.NewSetup();

        var result = setup.Service.UpdateAccount(Find(setup, LocalPhone).UserId, LocalChanges(name: "Tran Thi B"));

        Assert.True(result.Ok);
        Assert.Equal("Tran Thi B", Find(setup, LocalPhone).FullName);
    }

    [Fact]
    public void Hash_is_unchanged_when_no_new_password_is_given()
    {
        using var setup = AdminTestData.NewSetup();
        string before = Find(setup, LocalPhone).PasswordHash;

        setup.Service.UpdateAccount(Find(setup, LocalPhone).UserId, LocalChanges(name: "Tran Thi B"));

        Assert.Equal(before, Find(setup, LocalPhone).PasswordHash);
        Assert.True(setup.Service.Login(LocalPhone, DbSeeder.DemoPassword).Ok);
    }

    [Fact]
    public void New_password_is_hashed_and_used_for_sign_in()
    {
        using var setup = AdminTestData.NewSetup();
        string before = Find(setup, LocalPhone).PasswordHash;

        var result = setup.Service.UpdateAccount(Find(setup, LocalPhone).UserId, LocalChanges(password: "newpass123"));

        Assert.True(result.Ok);
        string after = Find(setup, LocalPhone).PasswordHash;
        Assert.NotEqual(before, after);
        Assert.DoesNotContain("newpass123", after); // never stored as plain text
        Assert.True(setup.Service.Login(LocalPhone, "newpass123").Ok);
        Assert.False(setup.Service.Login(LocalPhone, DbSeeder.DemoPassword).Ok);
    }

    [Fact]
    public void Short_new_password_is_rejected()
    {
        using var setup = AdminTestData.NewSetup();

        var result = setup.Service.UpdateAccount(Find(setup, LocalPhone).UserId, LocalChanges(password: "1234567"));

        Assert.False(result.Ok);
    }

    [Fact]
    public void Changed_phone_updates_the_profile_column_too()
    {
        using var setup = AdminTestData.NewSetup();

        var result = setup.Service.UpdateAccount(Find(setup, LocalPhone).UserId, LocalChanges(phone: "0312345678"));

        Assert.True(result.Ok);
        User user = Find(setup, "0312345678");
        Assert.Equal("0312345678", user.LocalProfile!.PhoneNumber);
        Assert.False(setup.Db.Users.Any(u => u.Identifier == LocalPhone)); // the old number is gone
    }

    [Fact]
    public void Identifier_of_another_account_is_rejected_and_nothing_changes()
    {
        using var setup = AdminTestData.NewSetup();
        AddLocal(setup, "0312345678");

        var result = setup.Service.UpdateAccount(Find(setup, LocalPhone).UserId, LocalChanges(name: "Changed", phone: "0312345678"));

        Assert.False(result.Ok);
        Assert.Contains("already registered", result.Errors[0]);
        User user = Find(setup, LocalPhone);
        Assert.Equal("Demo Local Customer", user.FullName);
        Assert.Equal(LocalPhone, user.LocalProfile!.PhoneNumber);
    }

    [Fact]
    public void Invalid_phone_is_rejected()
    {
        using var setup = AdminTestData.NewSetup();

        var result = setup.Service.UpdateAccount(Find(setup, LocalPhone).UserId, LocalChanges(phone: "0412345678"));

        Assert.False(result.Ok);
        Assert.Equal(LocalPhone, Find(setup, LocalPhone).Identifier);
    }

    [Fact]
    public void Tourist_passport_is_saved_in_upper_case_with_the_profile()
    {
        using var setup = AdminTestData.NewSetup();
        var changes = new AccountChanges
        {
            FullName = "John Smith",
            Identifier = "ab123456",
            Nationality = "British",
            Email = "john@example.com",
            HotelName = "  " // blank becomes empty (null)
        };

        var result = setup.Service.UpdateAccount(Find(setup, "DEMO12345").UserId, changes);

        Assert.True(result.Ok);
        User user = Find(setup, "AB123456");
        Assert.Equal("AB123456", user.TouristProfile!.PassportNo);
        Assert.Equal("john@example.com", user.TouristProfile.Email);
        Assert.Null(user.TouristProfile.HotelName);
    }

    [Fact]
    public void Kitchen_staff_can_change_hub()
    {
        using var setup = AdminTestData.NewSetup();
        Hub hub2 = setup.Db.Hubs.AsNoTracking().Single(h => h.Name == "Hub 2 - District 3");
        User kitchen = Find(setup, "KITCHEN1");

        var result = setup.Service.UpdateAccount(kitchen.UserId,
            new AccountChanges { FullName = kitchen.FullName, Identifier = "KITCHEN1", HubId = hub2.HubId });

        Assert.True(result.Ok);
        Assert.Equal(hub2.HubId, Find(setup, "KITCHEN1").HubId);
    }

    [Fact]
    public void Kitchen_staff_needs_an_active_hub()
    {
        using var setup = AdminTestData.NewSetup();
        Hub hub2 = setup.Db.Hubs.Single(h => h.Name == "Hub 2 - District 3");
        hub2.IsActive = false;
        setup.Db.SaveChanges();
        setup.Db.ChangeTracker.Clear();
        User kitchen = Find(setup, "KITCHEN1");

        var inactiveHub = setup.Service.UpdateAccount(kitchen.UserId,
            new AccountChanges { FullName = kitchen.FullName, Identifier = "KITCHEN1", HubId = hub2.HubId });
        var noHub = setup.Service.UpdateAccount(kitchen.UserId,
            new AccountChanges { FullName = kitchen.FullName, Identifier = "KITCHEN1", HubId = null });

        Assert.False(inactiveHub.Ok);
        Assert.False(noHub.Ok);
    }

    [Fact]
    public void Staff_id_must_be_valid()
    {
        using var setup = AdminTestData.NewSetup();

        var result = setup.Service.UpdateAccount(Find(setup, "ADMIN").UserId,
            new AccountChanges { FullName = "System Admin", Identifier = "AD" });

        Assert.False(result.Ok);
        Assert.Contains("Staff ID", result.Errors[0]);
    }

    [Fact]
    public void Unknown_account_cannot_be_edited()
    {
        using var setup = AdminTestData.NewSetup();

        var result = setup.Service.UpdateAccount(9999, LocalChanges());

        Assert.False(result.Ok);
    }

    [Fact]
    public void Account_info_has_no_password_hash()
    {
        Assert.DoesNotContain(typeof(AccountInfo).GetProperties(), p => p.Name.Contains("Password"));
    }

    // ---------- list ----------

    [Fact]
    public void List_filters_by_role_and_status()
    {
        using var setup = AdminTestData.NewSetup();
        setup.Service.DeactivateAccount(Find(setup, "SHIPPER1").UserId, AdminId(setup));

        var shippers = setup.Service.ListAccounts(new UserFilter { Role = Roles.Shipper });
        var inactive = setup.Service.ListAccounts(new UserFilter { Status = AccountStatus.Inactive });

        Assert.Equal(3, shippers.TotalCount);
        Assert.Single(inactive.Items);
        Assert.Equal("SHIPPER1", inactive.Items[0].Identifier);
    }

    [Fact]
    public void List_searches_name_identifier_and_tourist_email()
    {
        using var setup = AdminTestData.NewSetup();

        var byName = setup.Service.ListAccounts(new UserFilter { Search = "Demo Local" });
        var byIdentifier = setup.Service.ListAccounts(new UserFilter { Search = "SHIPPER" });
        var byEmail = setup.Service.ListAccounts(new UserFilter { Search = "tourist@example.com" });

        Assert.Single(byName.Items);
        Assert.Equal(LocalPhone, byName.Items[0].Identifier);
        Assert.Equal(3, byIdentifier.TotalCount);
        Assert.Single(byEmail.Items);
        Assert.Equal(Roles.Tourist, byEmail.Items[0].Role);
    }

    [Fact]
    public void List_paging_stays_inside_the_pages()
    {
        using var setup = AdminTestData.NewSetup(); // 8 demo accounts

        var page = setup.Service.ListAccounts(new UserFilter { Page = 99, PageSize = 3 });

        Assert.Equal(8, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(3, page.Page);   // 99 is moved to the last page
        Assert.Equal(2, page.Items.Count);
    }
}