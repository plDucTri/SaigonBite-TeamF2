using SaigonBite.Models.Entities;
using SaigonBite.Repositories;

namespace SaigonBite.Helpers;

/// <summary>
/// Creates the demo accounts when the app starts. Safe to run again: an account that already exists is skipped.
/// Passwords go through PasswordHasher, so the database only holds hashes.
/// Identifiers are saved in upper case because the login page upper-cases what the person types.
/// </summary>
public class DbSeeder
{
    public const string DemoPassword = "Saigon@123";

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly SaigonBiteContext _db;

    public DbSeeder(IUserRepository users, IPasswordHasher hasher, SaigonBiteContext db)
    {
        _users = users;
        _hasher = hasher;
        _db = db;
    }

    public void Seed()
    {
        AddStaff("ADMIN", "System Admin", Roles.Admin, null);

        // One kitchen staff member per hub. The hub is found by name (the ids can differ on each computer).
        AddKitchen("KITCHEN1", "Kitchen Staff Hub 1", "Hub 1 - District 1");
        AddKitchen("KITCHEN2", "Kitchen Staff Hub 2", "Hub 2 - District 3");
        AddKitchen("KITCHEN3", "Kitchen Staff Test Hub", "Test Hub - Capacity 100");

        AddStaff("SHIPPER1", "Shipper One", Roles.Shipper, null);
        AddStaff("SHIPPER2", "Shipper Two", Roles.Shipper, null);
        AddStaff("SHIPPER3", "Shipper Three", Roles.Shipper, null);

        AddLocalCustomer();
        AddTourist();
    }

    private void AddKitchen(string identifier, string fullName, string hubName)
    {
        int? hubId = _db.Hubs
            .Where(h => h.Name == hubName)
            .Select(h => (int?)h.HubId)
            .FirstOrDefault();

        // No hub yet (the hub script was not run): skip, it is created on the next start.
        if (hubId == null)
            return;

        AddStaff(identifier, fullName, Roles.Kitchen, hubId);
    }

    private void AddStaff(string identifier, string fullName, string role, int? hubId)
    {
        if (_users.ExistsByIdentifier(identifier))
            return;

        _users.Save(new User
        {
            Identifier = identifier,
            PasswordHash = _hasher.Hash(DemoPassword),
            Role = role,
            FullName = fullName,
            IsActive = true,
            HubId = hubId
        });
    }

    private void AddLocalCustomer()
    {
        const string phone = "0901234567";
        if (_users.ExistsByIdentifier(phone))
            return;

        _users.Save(new User
        {
            Identifier = phone,
            PasswordHash = _hasher.Hash(DemoPassword),
            Role = Roles.Local,
            FullName = "Demo Local Customer",
            IsActive = true,
            LocalProfile = new LocalProfile { PhoneNumber = phone }
        });
    }

    private void AddTourist()
    {
        const string passport = "DEMO12345";
        if (_users.ExistsByIdentifier(passport))
            return;

        _users.Save(new User
        {
            Identifier = passport,
            PasswordHash = _hasher.Hash(DemoPassword),
            Role = Roles.Tourist,
            FullName = "Demo Foreign Tourist",
            IsActive = true,
            TouristProfile = new TouristProfile
            {
                PassportNo = passport,
                Nationality = "British",
                Email = "tourist@example.com",
                HotelName = "Sunrise Hotel",
                RoomNo = "1205"
            }
        });
    }
}
