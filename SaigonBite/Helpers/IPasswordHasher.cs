namespace SaigonBite.Helpers;

public interface IPasswordHasher
{
    /// <summary>Returns the value stored in Users.PasswordHash (never the plain password).</summary>
    string Hash(string password);

    /// <summary>True only if the password matches the stored value. Never throws on bad data.</summary>
    bool Verify(string password, string stored);
}
