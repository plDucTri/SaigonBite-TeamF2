using SaigonBite.Validators;

namespace SaigonBite.Services;

public interface IAccountService
{
    /// <summary>Steps 3, 4 and 7 of the sequence diagram: validate, check uniqueness, send the code.</summary>
    ServiceResult<RegistrationStarted> RegisterAccount(AccountRequest request);

    /// <summary>Steps 8, 11 and 12: verify the code, hash the password, save the user and one profile.</summary>
    ServiceResult<AccountCreated> ConfirmRegistration(string identifier, string code);
}
