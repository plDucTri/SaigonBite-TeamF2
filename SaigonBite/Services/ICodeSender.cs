namespace SaigonBite.Services;

/// <summary>The "Code simulator" of the architecture diagram: delivers a verification code to the customer.</summary>
public interface ICodeSender
{
    /// <summary>
    /// Sends the code (OTP for Local, email code for Tourist). Returns the code only when it may be shown
    /// on screen (the simulated sender); a real sender would return null.
    /// </summary>
    string? Send(string identifier, string type, string code);
}
