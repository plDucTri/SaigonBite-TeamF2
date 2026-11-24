using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Repositories;
using SaigonBite.Tests.Services;

namespace SaigonBite.Tests.Helpers;

public class DbSeederTests
{
    private static void AddHubs(RegistrationTestSetup setup)
    {
        setup.Db.Hubs.Add(new Hub { Name = "Hub 1 - District 1", Address = "a", KitchenCapacity = 10, IsActive = true });
        setup.Db.Hubs.Add(new Hub { Name = "Hub 2 - District 3", Address = "b", KitchenCapacity = 20, IsActive = true });
        setup.Db.SaveChanges();
    }

    private static DbSeeder NewSeeder(RegistrationTestSetup setup) =>
        new(new UserRepository(setup.Db), setup.Hasher, setup.Db);

    [Fact]
    public void Seed_creates_every_role()
    {
        using var setup = new RegistrationTestSetup();
        AddHubs(setup);

        NewSeeder(setup).Seed();

        Assert.Equal(1, setup.Db.Users.Count(u => u.Role == Roles.Admin));
        Assert.Equal(2, setup.Db.Users.Count(u => u.Role == Roles.Kitchen)); // only 2 hubs exist in this test
        Assert.Equal(3, setup.Db.Users.Count(u => u.Role == Roles.Shipper));
        Assert.Equal(1, setup.Db.Users.Count(u => u.Role == Roles.Local));
        Assert.Equal(1, setup.Db.Users.Count(u => u.Role == Roles.Tourist));
    }

    [Fact]
    public void Running_the_seeder_twice_creates_no_duplicates()
    {
        using var setup = new RegistrationTestSetup();
        AddHubs(setup);

        NewSeeder(setup).Seed();
        int first = setup.Db.Users.Count();
        NewSeeder(setup).Seed();

        Assert.Equal(first, setup.Db.Users.Count());
    }

    [Fact]
    public void Kitchen_staff_belong_to_their_hub_and_other_roles_have_no_hub()
    {
        using var setup = new RegistrationTestSetup();
        AddHubs(setup);

        NewSeeder(setup).Seed();

        Assert.All(setup.Db.Users.Where(u => u.Role == Roles.Kitchen), u => Assert.NotNull(u.HubId));
        Assert.All(setup.Db.Users.Where(u => u.Role != Roles.Kitchen), u => Assert.Null(u.HubId));
    }

    [Fact]
    public void Kitchen_staff_are_skipped_when_the_hubs_do_not_exist()
    {
        using var setup = new RegistrationTestSetup();

        NewSeeder(setup).Seed();

        Assert.Equal(0, setup.Db.Users.Count(u => u.Role == Roles.Kitchen));
    }

    [Fact]
    public void Each_customer_has_exactly_one_profile()
    {
        using var setup = new RegistrationTestSetup();

        NewSeeder(setup).Seed();

        Assert.Equal(1, setup.Db.LocalProfiles.Count());
        Assert.Equal(1, setup.Db.TouristProfiles.Count());
    }

    [Fact]
    public void Passwords_are_saved_as_hashes_and_every_account_can_sign_in()
    {
        using var setup = new RegistrationTestSetup();
        AddHubs(setup);

        NewSeeder(setup).Seed();

        Assert.DoesNotContain(setup.Db.Users, u => u.PasswordHash.Contains(DbSeeder.DemoPassword));
        foreach (string id in new[] { "admin", "kitchen1", "shipper2", "0901234567", "demo12345" })
            Assert.True(setup.Service.Login(id, DbSeeder.DemoPassword).Ok, id);
    }
}
