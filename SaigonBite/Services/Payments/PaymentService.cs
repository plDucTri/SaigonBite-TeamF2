using SaigonBite.Models.Entities;

namespace SaigonBite.Services.Payments;

/// <summary>One line of the payment selector shown at checkout (Figma D03, D04, D18).</summary>
public record PaymentOption(string Method, bool Enabled, string? Note);

public class PaymentProcessResult
{
    public bool Success { get; init; }
    public RuleViolation? Violation { get; init; }
    public bool GatewayFailed { get; init; }
    public string Message { get; init; } = "";
    public string Method { get; init; } = "";
    public string Status { get; init; } = "";
}

public interface IPaymentService
{
    IReadOnlyList<PaymentOption> GetOptions(string customerRole, decimal total);
    PaymentProcessResult Process(string customerRole, string method, decimal total);
    void Refund(Payment payment);
}

/// <summary>BR13, BR14 and the simulated refund of BR16 and A-02. It only uses IPaymentStrategy, never a concrete gateway.</summary>
public class PaymentService : IPaymentService
{
    private readonly IPaymentStrategyFactory _factory;

    public PaymentService(IPaymentStrategyFactory factory)
    {
        _factory = factory;
    }

    public IReadOnlyList<PaymentOption> GetOptions(string customerRole, decimal total) =>
        _factory.MethodsFor(customerRole)
            .Select(m =>
            {
                bool ok = _factory.Create(m)!.IsAllowed(customerRole, total);
                return new PaymentOption(m, ok, ok ? null : "unavailable for this total");
            })
            .ToList();

    public PaymentProcessResult Process(string customerRole, string method, decimal total)
    {
        var strategy = _factory.Create(method);
        var name = _factory.CanonicalName(method);

        if (strategy == null || name == null || !strategy.IsAllowed(customerRole, 0m))
            return new PaymentProcessResult
            {
                Violation = new RuleViolation("BR13",
                    $"\"{method}\" is not a payment method for this account type.",
                    "Choose one of the payment methods offered at checkout.")
            };

        if (!strategy.IsAllowed(customerRole, total))
            return new PaymentProcessResult
            {
                Violation = new RuleViolation("BR14",
                    $"{name} cannot be used for a total of {total:N0} VND.",
                    "Choose another payment method.")
            };

        var result = strategy.Pay(total);
        return new PaymentProcessResult
        {
            Success = result.Success,
            GatewayFailed = !result.Success,
            Message = result.Message,
            Method = name,
            Status = result.Status
        };
    }

    /// <summary>
    /// BR16 and A-02: a cancelled or failed order paid online is refunded in full (simulated).
    /// A COD order has nothing to refund because no cash was collected.
    /// </summary>
    public void Refund(Payment payment)
    {
        if (payment.Status == PaymentStatus.Paid)
        {
            payment.Refunded = true;
            payment.Status = PaymentStatus.Refunded;
        }
        else if (payment.Status == PaymentStatus.Pending)
        {
            payment.Status = PaymentStatus.Voided;
        }
    }
}
