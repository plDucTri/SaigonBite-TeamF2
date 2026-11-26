namespace SaigonBite.Services.Payments;

public interface IPaymentStrategyFactory
{
    /// <summary>The strategy for this method name (matches Payments.Method), or null if the method does not exist.</summary>
    IPaymentStrategy? Create(string method);

    /// <summary>The method names a customer of this type may use (BR13), in display order.</summary>
    IReadOnlyList<string> MethodsFor(string customerRole);

    /// <summary>The canonical spelling of a method name (for example "momo" becomes "MoMo"), or null.</summary>
    string? CanonicalName(string method);
}

/// <summary>
/// CRC card PaymentStrategyFactory (BR13): chooses the payment class from the method name and the customer type.
/// Change Request recipe (NFR2, T-57): add ONE class implementing IPaymentStrategy and ONE line in the list below.
/// </summary>
public class PaymentStrategyFactory : IPaymentStrategyFactory
{
    private readonly Dictionary<string, IPaymentStrategy> _strategies;

    /// <summary>Normal use: the five methods of the specification.</summary>
    public PaymentStrategyFactory(IGatewaySimulator gateway)
        : this(new Dictionary<string, IPaymentStrategy>
        {
            ["MoMo"] = new MoMoPayment(gateway),
            ["VNPay"] = new VNPayPayment(gateway),
            ["Apple Pay"] = new ApplePayPayment(gateway),
            ["PayPal"] = new PayPalPayment(gateway),
            ["COD"] = new CodPayment(),
            // ["ZaloPay"] = new ZaloPayPayment(gateway),   <- a new method is one new class and one line like this
        })
    {
    }

    /// <summary>For tests: supply your own set of methods.</summary>
    public PaymentStrategyFactory(IDictionary<string, IPaymentStrategy> strategies)
    {
        _strategies = new Dictionary<string, IPaymentStrategy>(strategies, StringComparer.OrdinalIgnoreCase);
    }

    public IPaymentStrategy? Create(string method) =>
        _strategies.TryGetValue((method ?? string.Empty).Trim(), out var strategy) ? strategy : null;

    public IReadOnlyList<string> MethodsFor(string customerRole) =>
        _strategies.Where(kv => kv.Value.IsAllowed(customerRole, 0m)).Select(kv => kv.Key).ToList();

    public string? CanonicalName(string method) =>
        _strategies.Keys.FirstOrDefault(k => string.Equals(k, (method ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
}
