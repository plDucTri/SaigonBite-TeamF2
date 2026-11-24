using Microsoft.Extensions.Caching.Memory;
using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Repositories;
using SaigonBite.Validators;

namespace SaigonBite.Services;

/// <summary>
/// Services never touch the database: they use IUserRepository. Register as scoped.
/// The pending request (with the plain password) stays in server memory for up to 10 minutes.
/// It is never logged and never written to the database.
/// </summary>
public class AccountService : IAccountService
{
    private static readonly TimeSpan PendingLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(30);

    private readonly IAccountValidator _validator;
    private readonly IUserRepository _users;
    private readonly IVerificationService _verification;
    private readonly IPasswordHasher _hasher;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _time;

    public AccountService(
        IAccountValidator validator,
        IUserRepository users,
        IVerificationService verification,
        IPasswordHasher hasher,
        IMemoryCache cache,
        TimeProvider time)
    {
        _validator = validator;
        _users = users;
        _verification = verification;
        _hasher = hasher;
        _cache = cache;
        _time = time;
    }

    public ServiceResult<RegistrationStarted> RegisterAccount(AccountRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        AccountRequest clean = Normalize(request);

        // Step 3: validate(request)
        IReadOnlyList<string> errors = _validator.Validate(clean);
        if (errors.Count > 0)
            return ServiceResult<RegistrationStarted>.Fail(errors);

        // Step 4: existsByIdentifier(identifier)
        if (_users.ExistsByIdentifier(clean.Identifier))
            return ServiceResult<RegistrationStarted>.Fail(DuplicateMessage(clean.Type));

        // Step 7: sendCode(identifier, type)
        string? demoCode = _verification.SendCode(clean.Identifier, clean.Type);
        _cache.Set(PendingKey(clean.Identifier), clean, PendingLifetime);
        MarkSent(clean.Identifier);

        return ServiceResult<RegistrationStarted>.Success(new RegistrationStarted(clean.Identifier, demoCode));
    }

    public ServiceResult<RegistrationStarted> ResendCode(string identifier)
    {
        string id = NormalizeIdentifier(identifier);

        if (!_cache.TryGetValue(PendingKey(id), out AccountRequest? pending) || pending is null)
            return ServiceResult<RegistrationStarted>.Fail("The registration has expired. Please register again.");

        // The cooldown stops a person from resetting the 5-try limit again and again.
        if (_cache.TryGetValue(SentKey(id), out DateTimeOffset lastSent)
            && _time.GetUtcNow() - lastSent < ResendCooldown)
            return ServiceResult<RegistrationStarted>.Fail("Please wait 30 seconds before requesting another code.");

        string? demoCode = _verification.SendCode(id, pending.Type);
        MarkSent(id);

        return ServiceResult<RegistrationStarted>.Success(new RegistrationStarted(id, demoCode));
    }

    public ServiceResult<AccountCreated> ConfirmRegistration(string identifier, string code)
    {
        string id = NormalizeIdentifier(identifier);

        if (!_cache.TryGetValue(PendingKey(id), out AccountRequest? pending) || pending is null)
            return ServiceResult<AccountCreated>.Fail("The registration has expired. Please register again.");

        // Step 8: verifyCode(identifier, code)
        if (!_verification.VerifyCode(id, (code ?? string.Empty).Trim()))
            return ServiceResult<AccountCreated>.Fail("The verification code is wrong or has expired.");

        // Someone may have taken the identifier while the code was pending.
        if (_users.ExistsByIdentifier(id))
        {
            _cache.Remove(PendingKey(id));
            return ServiceResult<AccountCreated>.Fail(DuplicateMessage(pending.Type));
        }

        // Step 11: hash(password), only after the code is verified
        string hash = _hasher.Hash(pending.Password);

        // Steps 12-14: save(user, profile, role, active = true) in one transaction
        User user = BuildUser(pending, hash);
        _users.Save(user);

        _cache.Remove(PendingKey(id));
        _cache.Remove(SentKey(id));
        return ServiceResult<AccountCreated>.Success(new AccountCreated(user.UserId));
    }

    public ServiceResult<SignedInUser> Login(string identifier, string password)
    {
        // One message for every failure, so nobody can tell which part was wrong.
        const string failed = "Invalid identifier or password.";

        User? user = _users.GetByIdentifier(NormalizeIdentifier(identifier));

        if (user is null || !user.IsActive || !_hasher.Verify(password ?? string.Empty, user.PasswordHash))
            return ServiceResult<SignedInUser>.Fail(failed);

        return ServiceResult<SignedInUser>.Success(new SignedInUser(user.UserId, user.FullName, user.Role));
    }

    private static User BuildUser(AccountRequest request, string passwordHash)
    {
        var user = new User
        {
            Identifier = request.Identifier,
            PasswordHash = passwordHash,
            Role = request.Type,
            FullName = request.FullName,
            IsActive = true
        };

        // Exactly one profile, never both.
        if (request.Type == Roles.Local)
        {
            user.LocalProfile = new LocalProfile { PhoneNumber = request.Identifier };
        }
        else
        {
            user.TouristProfile = new TouristProfile
            {
                PassportNo = request.Identifier,
                Nationality = request.Nationality,
                Email = request.Email,
                HotelName = request.HotelName,
                RoomNo = request.RoomNo
            };
        }

        return user;
    }

    private static AccountRequest Normalize(AccountRequest request) => new()
    {
        Type = (request.Type ?? string.Empty).Trim(),
        Identifier = NormalizeIdentifier(request.Identifier),
        FullName = (request.FullName ?? string.Empty).Trim(),
        Password = request.Password ?? string.Empty, // spaces in a password are allowed
        Nationality = (request.Nationality ?? string.Empty).Trim(),
        Email = (request.Email ?? string.Empty).Trim(),
        HotelName = string.IsNullOrWhiteSpace(request.HotelName) ? null : request.HotelName.Trim(),
        RoomNo = string.IsNullOrWhiteSpace(request.RoomNo) ? null : request.RoomNo.Trim()
    };

    // Phone numbers have no letters, so upper-casing only changes passports (stored in upper case).
    private static string NormalizeIdentifier(string? identifier) =>
        (identifier ?? string.Empty).Trim().ToUpperInvariant();

    private static string DuplicateMessage(string type) => type == Roles.Local
        ? "This phone number is already registered."
        : "This passport number is already registered.";

    private void MarkSent(string identifier) => _cache.Set(SentKey(identifier), _time.GetUtcNow(), PendingLifetime);

    private static string PendingKey(string identifier) => $"pending:{identifier}";

    private static string SentKey(string identifier) => $"sent:{identifier}";
}
