using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Services;

namespace SaigonBite.Tests.Services;

public class AccountStaffTests
{
    private const string LocalPhone = "0901234567"; // the seeded Local customer

    private static User Find(RegistrationTestSetup setup, string identifier) => AdminTestData.Find(setup, identifier);

    private static int AdminId(RegistrationTestSetup setup) => Find(setup, "ADMIN").UserId;

    private static NewStaffAccount Kitchen(RegistrationTestSetup setup) => new()
    {
        FullName = "Pham Van Duc",
        Role = Roles.Kitchen,
        Identifier = "staff-014",
        Password = "temp12345",
        HubId = setup.Db.Hubs.First().HubId
    };

    // ---------- reactivate and session check ----------

    [Fact]
    public void Reactivate_lets_the_account_sign_in_again()
    {
        using var setup = AdminTestData.NewSetup();
        int id = Find(setup, LocalPhone).UserId;
        setup.Service.DeactivateAccount(id, AdminId(setup));

        var result = setup.Service.ReactivateAccount(id);

        Assert.True(result.Ok);
        Assert.True(Find(setup, LocalPhone).IsActive);
        Assert.True(setup.Service.Login(LocalPhone, DbSeeder.DemoPassword).Ok);
    }

    [Fact]
    public void Reactivate_fails_for_an_active_or_unknown_account()
    {
        using var setup = AdminTestData.NewSetup();

        Assert.False(setup.Service.ReactivateAccount(Find(setup, LocalPhone).UserId).Ok);
        Assert.False(setup.Service.ReactivateAccount(9999).Ok);
    }

    [Fact]
    public void IsAccountActive_follows_the_flag()
    {
        using var setup = AdminTestData.NewSetup();
        int id = Find(setup, LocalPhone).UserId;

        Assert.True(setup.Service.IsAccountActive(id));

        setup.Service.DeactivateAccount(id, AdminId(setup));

        Assert.False(setup.Service.IsAccountActive(id));
        Assert.False(setup.Service.IsAccountActive(9999)); // an unknown account is not active
    }

    // ---------- create staff ----------

    [Fact]
    public void Create_kitchen_staff_saves_a_hashed_password_and_the_hub()
    {
        using var setup = AdminTestData.NewSetup();
        NewStaffAccount request = Kitchen(setup);

        var result = setup.Service.CreateStaffAccount(request);

        Assert.True(result.Ok);
        User user = Find(setup, "STAFF-014"); // saved in upper case
        Assert.Equal(Roles.Kitchen, user.Role);
        Assert.Equal(request.HubId, user.HubId);
        Assert.True(user.IsActive);
        Assert.DoesNotContain("temp12345", user.PasswordHash);
        Assert.True(setup.Service.Login("staff-014", "temp12345").Ok);
    }

    [Fact]
    public void Create_shipper_ignores_the_hub()
    {
        using var setup = AdminTestData.NewSetup();
        NewStaffAccount request = Kitchen(setup);
        request.Role = Roles.Shipper;

        var result = setup.Service.CreateStaffAccount(request);

        Assert.True(result.Ok);
        Assert.Null(Find(setup, "STAFF-014").HubId);
    }

    [Fact]
    public void Create_kitchen_staff_needs_a_hub()
    {
        using var setup = AdminTestData.NewSetup();
        NewStaffAccount request = Kitchen(setup);
        request.HubId = null;

        var result = setup.Service.CreateStaffAccount(request);

        Assert.False(result.Ok);
        Assert.False(setup.Db.Users.Any(u => u.Identifier == "STAFF-014"));
    }

    [Fact]
    public void Create_with_an_existing_staff_id_is_rejected()
    {
        using var setup = AdminTestData.NewSetup();
        NewStaffAccount request = Kitchen(setup);
        request.Identifier = "admin";

        var result = setup.Service.CreateStaffAccount(request);

        Assert.False(result.Ok);
        Assert.Contains("already in use", result.Errors[0]);
        Assert.Equal(1, setup.Db.Users.Count(u => u.Identifier == "ADMIN"));
    }

    [Fact]
    public void Create_does_not_make_customer_accounts()
    {
        using var setup = AdminTestData.NewSetup();
        NewStaffAccount request = Kitchen(setup);
        request.Role = Roles.Local;

        var result = setup.Service.CreateStaffAccount(request);

        Assert.False(result.Ok);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    public void Create_needs_a_password_of_at_least_8_characters(string password)
    {
        using var setup = AdminTestData.NewSetup();
        NewStaffAccount request = Kitchen(setup);
        request.Password = password;

        var result = setup.Service.CreateStaffAccount(request);

        Assert.False(result.Ok);
        Assert.Contains("at least 8", result.Errors[0]);
    }
}