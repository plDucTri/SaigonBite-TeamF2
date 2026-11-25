using SaigonBite.Helpers;
using SaigonBite.Models.Entities;

namespace SaigonBite.Repositories;

/// <summary>The only class that uses Entity Framework for users. FindUsers and SetActive come in the admin commit.</summary>
public interface IUserRepository
{
    /// <summary>
    /// True if a user with this identifier exists. Ignores IsActive, so a deactivated phone or passport
    /// cannot be registered again. exceptUserId excludes the account being edited.
    /// </summary>
    bool ExistsByIdentifier(string identifier, int? exceptUserId = null);

    User? GetByIdentifier(string identifier);

    User? GetById(int userId);

    /// <summary>Adds a new user (UserId = 0) or saves changes to an existing one. One SaveChanges = one transaction.</summary>
    void Save(User user);

    /// <summary>Finds users for the admin list: search text, role and status filters, one page at a time.</summary>
    PagedResult<User> FindUsers(UserFilter filter);

    /// <summary>Sets IsActive and saves. Returns false if the user does not exist. The row is never deleted.</summary>
    bool SetActive(int userId, bool active);

    /// <summary>Active hubs for the Assigned Hub drop-down (kept here until a shared HubRepository exists).</summary>
    IReadOnlyList<Hub> GetActiveHubs();
}
