using Microsoft.EntityFrameworkCore;
using SaigonBite.Models.Entities;

namespace SaigonBite.Repositories;

public class UserRepository : IUserRepository
{
    private readonly SaigonBiteContext _db;

    public UserRepository(SaigonBiteContext db)
    {
        _db = db;
    }

    public bool ExistsByIdentifier(string identifier, int? exceptUserId = null)
    {
        return _db.Users.Any(u => u.Identifier == identifier
                                  && (exceptUserId == null || u.UserId != exceptUserId));
    }

    public User? GetByIdentifier(string identifier)
    {
        return _db.Users
            .Include(u => u.LocalProfile)
            .Include(u => u.TouristProfile)
            .FirstOrDefault(u => u.Identifier == identifier);
    }

    public User? GetById(int userId)
    {
        return _db.Users
            .Include(u => u.LocalProfile)
            .Include(u => u.TouristProfile)
            .FirstOrDefault(u => u.UserId == userId);
    }

    public void Save(User user)
    {
        if (user.UserId == 0)
            _db.Users.Add(user); // the profile attached to the user is added in the same SaveChanges

        _db.SaveChanges();
    }
}
