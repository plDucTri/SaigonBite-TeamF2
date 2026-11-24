namespace SaigonBite.Services;

/// <summary>Returned when the code has been sent. DemoCode is shown on screen only because the sender is simulated.</summary>
public record RegistrationStarted(string Identifier, string? DemoCode);

/// <summary>Returned when the account has been saved (AccountCreated(userId) in the sequence diagram).</summary>
public record AccountCreated(int UserId);
/// <summary>What the cookie needs after a successful login.</summary>
public record SignedInUser(int UserId, string FullName, string Role);