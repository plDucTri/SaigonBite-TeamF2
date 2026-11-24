using System.Net.Mail;
using System.Text.RegularExpressions;
using SaigonBite.Helpers;

namespace SaigonBite.Validators;

public class AccountValidator : IAccountValidator
{
    public const int MinPasswordLength = 8;

    // [0-9] instead of \d (in .NET \d also matches non-ASCII digits); \z instead of $ (no trailing newline).
    private static readonly Regex LocalPhone =
        new(@"^0[35789][0-9]{8}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TouristPassport =
        new(@"^[A-Za-z0-9]{6,9}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<string> Validate(AccountRequest request, bool passwordRequired = true)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();

        // Rules chosen by customer type (BR18)
        switch (request.Type)
        {
            case Roles.Local:
                if (!LocalPhone.IsMatch(request.Identifier ?? string.Empty))
                    errors.Add("Phone number must have 10 digits and start with 03, 05, 07, 08 or 09.");
                break;

            case Roles.Tourist:
                if (!TouristPassport.IsMatch(request.Identifier ?? string.Empty))
                    errors.Add("Passport number must have 6 to 9 letters or digits.");
                if (string.IsNullOrWhiteSpace(request.Nationality))
                    errors.Add("Nationality is required.");
                if (!IsValidEmail(request.Email))
                    errors.Add("Email address is not valid.");
                break;

            default:
                errors.Add("Customer type must be Local or Tourist.");
                break;
        }

        // Rules for both types
        if (string.IsNullOrWhiteSpace(request.FullName))
            errors.Add("Full name is required.");

        string password = request.Password ?? string.Empty;
        if (password.Length == 0)
        {
            if (passwordRequired)
                errors.Add($"Password must have at least {MinPasswordLength} characters.");
        }
        else if (password.Length < MinPasswordLength)
        {
            errors.Add($"Password must have at least {MinPasswordLength} characters.");
        }

        return errors;
    }

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        string trimmed = email.Trim();
        try
        {
            var address = new MailAddress(trimmed);
            // MailAddress also accepts "Name <a@b.com>"; require the plain address only.
            return string.Equals(address.Address, trimmed, StringComparison.OrdinalIgnoreCase)
                   && address.Host.Contains('.');
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
