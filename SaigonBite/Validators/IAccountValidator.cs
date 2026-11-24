namespace SaigonBite.Validators;

public interface IAccountValidator
{
    /// <summary>
    /// Applies the BR18 rules for request.Type and returns every violation (empty list = valid).
    /// Set passwordRequired to false when editing: an empty password then means "keep the old one",
    /// but a non-empty password must still have at least 8 characters.
    /// </summary>
    IReadOnlyList<string> Validate(AccountRequest request, bool passwordRequired = true);
}
