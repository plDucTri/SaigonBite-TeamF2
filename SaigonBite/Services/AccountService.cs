using Microsoft.Extensions.Caching.Memory;
using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Repositories;
using SaigonBite.Validators;
using System.Text.RegularExpressions;

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
    // Staff IDs such as ADMIN, KITCHEN1 or STAFF-001 (the text is upper-cased before this check).
    private static readonly Regex StaffId = new(@"^[A-Z0-9-]{3,30}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

        // validate(request)
        IReadOnlyList<string> errors = _validator.Validate(clean);
        if (errors.Count > 0)
            return ServiceResult<RegistrationStarted>.Fail(errors);

        // existsByIdentifier(identifier)
        if (_users.ExistsByIdentifier(clean.Identifier))
            return ServiceResult<RegistrationStarted>.Fail(DuplicateMessage(clean.Type));

        // sendCode(identifier, type)
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

        // verifyCode(identifier, code)
        if (!_verification.VerifyCode(id, (code ?? string.Empty).Trim()))
            return ServiceResult<AccountCreated>.Fail("The verification code is wrong or has expired.");

        // Someone may have taken the identifier while the code was pending.
        if (_users.ExistsByIdentifier(id))
        {
            _cache.Remove(PendingKey(id));
            return ServiceResult<AccountCreated>.Fail(DuplicateMessage(pending.Type));
        }

        // hash(password), only after the code is verified
        string hash = _hasher.Hash(pending.Password);

        // save(user, profile, role, active = true) in one transaction
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

    public PagedResult<AccountInfo> ListAccounts(UserFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        PagedResult<User> page = _users.FindUsers(filter);
        List<AccountInfo> rows = page.Items.Select(ToInfo).ToList();

        return new PagedResult<AccountInfo>(rows, page.TotalCount, page.Page, page.PageSize);
    }

    public ServiceResult<AccountInfo> GetAccount(int userId)
    {
        User? user = _users.GetById(userId);
        if (user is null)
            return ServiceResult<AccountInfo>.Fail("Account not found.");

        return ServiceResult<AccountInfo>.Success(ToInfo(user));
    }

    public IReadOnlyList<HubOption> GetHubOptions()
    {
        return _users.GetActiveHubs().Select(h => new HubOption(h.HubId, h.Name)).ToList();
    }
    // updateAccount(userId, changes)
    public ServiceResult<AccountInfo> UpdateAccount(int userId, AccountChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        User? user = _users.GetById(userId);
        if (user is null)
            return ServiceResult<AccountInfo>.Fail("Account not found.");
        // an inactive account cannot be edited
        if (!user.IsActive)
            return ServiceResult<AccountInfo>.Fail("This account is inactive. Reactivate it first.");

        // Tidy the text. The identifier is stored in upper case.
        string identifier = NormalizeIdentifier(changes.Identifier);
        string fullName = (changes.FullName ?? string.Empty).Trim();
        string newPassword = changes.NewPassword ?? string.Empty; // spaces in a password are allowed
        string nationality = (changes.Nationality ?? string.Empty).Trim();
        string email = (changes.Email ?? string.Empty).Trim();
        string? hotelName = string.IsNullOrWhiteSpace(changes.HotelName) ? null : changes.HotelName.Trim();
        string? roomNo = string.IsNullOrWhiteSpace(changes.RoomNo) ? null : changes.RoomNo.Trim();

        // Validate(changes) with the rules for this account's role
        var errors = new List<string>();
        if (user.Role == Roles.Local || user.Role == Roles.Tourist)
        {
            var request = new AccountRequest
            {
                Type = user.Role,
                Identifier = identifier,
                FullName = fullName,
                Password = newPassword,
                Nationality = nationality,
                Email = email,
                HotelName = hotelName,
                RoomNo = roomNo
            };
            // passwordRequired: false, so an empty password means "keep the old one"
            errors.AddRange(_validator.Validate(request, passwordRequired: false));
        }
        else
        {
            errors.AddRange(ValidateStaff(user.Role, identifier, fullName, newPassword, changes.HubId));
        }

        if (errors.Count > 0)
            return ServiceResult<AccountInfo>.Fail(errors);

        // The identifier must stay unique. exceptUserId lets the account keep its own identifier.
        if (_users.ExistsByIdentifier(identifier, exceptUserId: userId))
            return ServiceResult<AccountInfo>.Fail(DuplicateMessage(user.Role));

        // Everything is valid, so now copy the changes onto the user.
        user.Identifier = identifier;
        user.FullName = fullName;

        // hash(newPassword), only if a new password was typed
        if (newPassword.Length > 0)
            user.PasswordHash = _hasher.Hash(newPassword);

        // Keep the profile column in step with the identifier.
        if (user.Role == Roles.Local && user.LocalProfile is not null)
        {
            user.LocalProfile.PhoneNumber = identifier;
        }

        if (user.Role == Roles.Tourist && user.TouristProfile is not null)
        {
            user.TouristProfile.PassportNo = identifier;
            user.TouristProfile.Nationality = nationality;
            user.TouristProfile.Email = email;
            user.TouristProfile.HotelName = hotelName;
            user.TouristProfile.RoomNo = roomNo;
        }

        if (user.Role == Roles.Kitchen)
            user.HubId = changes.HubId;

        // save(user, profile). One SaveChanges updates the same rows.
        _users.Save(user);

        // Read it again so the hub name is current.
        return ServiceResult<AccountInfo>.Success(ToInfo(_users.GetById(userId)!));
    }

    // Rules for Kitchen, Shipper and Admin accounts (BR18 only covers customers).
    private List<string> ValidateStaff(string role, string identifier, string fullName, string newPassword, int? hubId)
    {
        var errors = new List<string>();

        if (!StaffId.IsMatch(identifier))
            errors.Add("Staff ID must have 3 to 30 letters, digits or hyphens.");

        if (fullName.Length == 0)
            errors.Add("Full name is required.");

        if (newPassword.Length > 0 && newPassword.Length < AccountValidator.MinPasswordLength)
            errors.Add($"Password must have at least {AccountValidator.MinPasswordLength} characters.");

        // Only kitchen staff belong to a hub, and it must be one of the active hubs.
        if (role == Roles.Kitchen && (hubId is null || !_users.GetActiveHubs().Any(h => h.HubId == hubId)))
            errors.Add("Choose an assigned hub for kitchen staff.");

        return errors;
    }
    // deactivateAccount(userId) -> setActive(userId, false)
    public ServiceResult<AccountInfo> DeactivateAccount(int userId, int actingAdminId)
    {
        if (userId == actingAdminId)
            return ServiceResult<AccountInfo>.Fail("You cannot deactivate your own account.");

        User? user = _users.GetById(userId);
        if (user is null)
            return ServiceResult<AccountInfo>.Fail("Account not found.");

        if (!user.IsActive)
            return ServiceResult<AccountInfo>.Fail("This account is already inactive.");

        _users.SetActive(userId, false); // the row and its orders stay

        return ServiceResult<AccountInfo>.Success(ToInfo(user));
    }
    public ServiceResult<AccountInfo> ReactivateAccount(int userId)
    {
        User? user = _users.GetById(userId);
        if (user is null)
            return ServiceResult<AccountInfo>.Fail("Account not found.");

        if (user.IsActive)
            return ServiceResult<AccountInfo>.Fail("This account is already active.");

        _users.SetActive(userId, true);

        return ServiceResult<AccountInfo>.Success(ToInfo(user));
    }
    public bool IsAccountActive(int userId)
    {
        return _users.GetById(userId)?.IsActive == true;
    }
    public ServiceResult<AccountCreated> CreateStaffAccount(NewStaffAccount request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string role = (request.Role ?? string.Empty).Trim();
        string identifier = NormalizeIdentifier(request.Identifier);
        string fullName = (request.FullName ?? string.Empty).Trim();
        string password = request.Password ?? string.Empty;

        // Customers register themselves, so only staff roles are allowed here.
        if (role != Roles.Kitchen && role != Roles.Shipper && role != Roles.Admin)
            return ServiceResult<AccountCreated>.Fail("Role must be Kitchen Staff, Shipper or Admin.");

        List<string> errors = ValidateStaff(role, identifier, fullName, password, request.HubId);
        if (password.Length == 0)
            errors.Add($"Password must have at least {AccountValidator.MinPasswordLength} characters."); // a new account needs one

        if (errors.Count > 0)
            return ServiceResult<AccountCreated>.Fail(errors);

        if (_users.ExistsByIdentifier(identifier))
            return ServiceResult<AccountCreated>.Fail(DuplicateMessage(role));

        var user = new User
        {
            Identifier = identifier,
            PasswordHash = _hasher.Hash(password),
            Role = role,
            FullName = fullName,
            IsActive = true,
            HubId = role == Roles.Kitchen ? request.HubId : null // only kitchen staff have a hub
        };
        _users.Save(user);

        return ServiceResult<AccountCreated>.Success(new AccountCreated(user.UserId));
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
    // Copies only what the admin pages need. The hash is left out on purpose.
    private static AccountInfo ToInfo(User u) => new(
        u.UserId, u.Identifier, u.FullName, u.Role, u.IsActive, u.CreatedAt,
        u.TouristProfile?.Email, u.TouristProfile?.Nationality,
        u.TouristProfile?.HotelName, u.TouristProfile?.RoomNo,
        u.HubId, u.Hub?.Name);
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

    private static string DuplicateMessage(string type)
    {
        if (type == Roles.Local)
            return "This phone number is already registered.";
        if (type == Roles.Tourist)
            return "This passport number is already registered.";
        return "This staff ID is already in use.";
    }

    private void MarkSent(string identifier) => _cache.Set(SentKey(identifier), _time.GetUtcNow(), PendingLifetime);

    private static string PendingKey(string identifier) => $"pending:{identifier}";

    private static string SentKey(string identifier) => $"sent:{identifier}";
}
