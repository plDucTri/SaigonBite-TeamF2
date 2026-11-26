namespace SaigonBite.Services.Payments;

/// <summary>Outcome of one payment attempt. Status is stored in Payments.Status when Success is true.</summary>
public record PaymentResult(bool Success, string Status, string Message);

/// <summary>
/// Strategy pattern for BR13 and BR14 (report 2.4, "IsAllowed, Pay"): one interface and one class per method.
/// A new method is one new class plus one line in PaymentStrategyFactory; no existing payment class is edited (NFR2).
/// </summary>
public interface IPaymentStrategy
{
    /// <summary>
    /// May a customer of this type (Users.Role: "Local" or "Tourist") pay this total with this method?
    /// BR13 decides by customer type; BR14 adds the COD limit. IsAllowed(role, 0) says whether the role may use
    /// the method at all (the factory uses it to list the methods of a customer type).
    /// </summary>
    bool IsAllowed(string customerRole, decimal total);

    /// <summary>Takes the payment (simulated, spec section G).</summary>
    PaymentResult Pay(decimal amount);
}
