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
}
