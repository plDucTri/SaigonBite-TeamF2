using SaigonBite.Helpers;
using SaigonBite.Repositories;
using SaigonBite.Validators;

namespace SaigonBite.Services;

public interface IAccountService
{
    /// <summary>Validate, check uniqueness, send the code.</summary>
    ServiceResult<RegistrationStarted> RegisterAccount(AccountRequest request);

    /// <summary>Verify the code, hash the password, save the user and one profile.</summary>
    ServiceResult<AccountCreated> ConfirmRegistration(string identifier, string code);

    /// <summary>Sends a new code for a registration that is still pending (30 seconds between codes).</summary>
    ServiceResult<RegistrationStarted> ResendCode(string identifier);

    /// <summary>Checks identifier and password. Only an active account with a matching hash gets in.</summary>
    ServiceResult<SignedInUser> Login(string identifier, string password);

    /// <summary>Admin list: one page of accounts for the search text, role and status filters.</summary>
    PagedResult<AccountInfo> ListAccounts(UserFilter filter);

    /// <summary>One account for the View and Edit pages.</summary>
    ServiceResult<AccountInfo> GetAccount(int userId);

    /// <summary>Hubs for the Assigned Hub drop-down.</summary>
    IReadOnlyList<HubOption> GetHubOptions();

    /// <summary>Validates and saves the changes. The identifier must stay unique. The password is hashed only if a new one is given.</summary>
    ServiceResult<AccountInfo> UpdateAccount(int userId, AccountChanges changes);

    /// <summary>Soft delete: sets IsActive to false and keeps the row. An admin cannot deactivate their own account.</summary>
    ServiceResult<AccountInfo> DeactivateAccount(int userId, int actingAdminId);
}
