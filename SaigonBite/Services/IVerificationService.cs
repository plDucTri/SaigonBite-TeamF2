namespace SaigonBite.Services;

public interface IVerificationService
{
    /// <summary>Creates a 6-digit code valid for 5 minutes and hands it to the code sender.</summary>
    string? SendCode(string identifier, string type);

    /// <summary>True if the code is right and not expired. A code works once and locks after 5 wrong tries.</summary>
    bool VerifyCode(string identifier, string code);
}
