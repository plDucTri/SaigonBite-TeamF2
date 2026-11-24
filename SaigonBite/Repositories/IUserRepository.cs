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
}
