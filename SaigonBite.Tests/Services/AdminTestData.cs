using Microsoft.EntityFrameworkCore;
using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Repositories;

namespace SaigonBite.Tests.Services;

/// <summary>Shared set-up for the admin account tests: two hubs plus the demo accounts.</summary>
public static class AdminTestData
{
    public static RegistrationTestSetup NewSetup()
    {
        var setup = new RegistrationTestSetup();
        setup.Db.Hubs.Add(new Hub { Name = "Hub 1 - District 1", Address = "a", KitchenCapacity = 10, IsActive = true });
        setup.Db.Hubs.Add(new Hub { Name = "Hub 2 - District 3", Address = "b", KitchenCapacity = 20, IsActive = true });
        setup.Db.SaveChanges();

        // Creates ADMIN, KITCHEN1, KITCHEN2, SHIPPER1-3, the Local 0901234567 and the Tourist DEMO12345.
        new DbSeeder(new UserRepository(setup.Db), setup.Hasher, setup.Db).Seed();
        setup.Db.ChangeTracker.Clear();
        return setup;
    }

    /// <summary>Reads a user straight from the database (not the tracked copy), with its profiles.</summary>
    public static User Find(RegistrationTestSetup setup, string identifier) =>
        setup.Db.Users
            .AsNoTracking()
            .Include(u => u.LocalProfile)
            .Include(u => u.TouristProfile)
            .Single(u => u.Identifier == identifier);
}