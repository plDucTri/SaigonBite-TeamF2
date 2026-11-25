using Microsoft.EntityFrameworkCore;
using SaigonBite.Helpers;
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
            .Include(u => u.Hub)
            .FirstOrDefault(u => u.UserId == userId);
    }

    public void Save(User user)
    {
        if (user.UserId == 0)
            _db.Users.Add(user); // the profile attached to the user is added in the same SaveChanges

        _db.SaveChanges();
    }

    public PagedResult<User> FindUsers(UserFilter filter)
    {
        IQueryable<User> query = _db.Users
            .Include(u => u.LocalProfile)
            .Include(u => u.TouristProfile)
            .Include(u => u.Hub);

        // Search in name, identifier (phone or passport) and tourist email.
        // SQL Server ignores letter case here.
        string search = (filter.Search ?? string.Empty).Trim();
        if (search.Length > 0)
        {
            query = query.Where(u => u.FullName.Contains(search)
                                     || u.Identifier.Contains(search)
                                     || (u.TouristProfile != null && u.TouristProfile.Email.Contains(search)));
        }

        string role = (filter.Role ?? string.Empty).Trim();
        if (role.Length > 0)
            query = query.Where(u => u.Role == role);

        if (filter.Status == AccountStatus.Active)
            query = query.Where(u => u.IsActive);
        else if (filter.Status == AccountStatus.Inactive)
            query = query.Where(u => !u.IsActive);

        // Paging: count first, then keep the page number inside 1..TotalPages.
        int pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;
        int total = query.Count();
        int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        int page = Math.Clamp(filter.Page, 1, totalPages);

        List<User> items = query
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<User>(items, total, page, pageSize);
    }

    public bool SetActive(int userId, bool active)
    {
        User? user = _db.Users.FirstOrDefault(u => u.UserId == userId);
        if (user is null)
            return false;

        user.IsActive = active; // soft delete: the row stays
        _db.SaveChanges();
        return true;
    }

    public IReadOnlyList<Hub> GetActiveHubs()
    {
        return _db.Hubs.Where(h => h.IsActive).OrderBy(h => h.Name).ToList();
    }
}