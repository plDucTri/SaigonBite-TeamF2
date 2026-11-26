using SaigonBite.Helpers;

namespace SaigonBite.Services.Payments;

/// <summary>Shared behaviour of the four simulated online gateways: approve a positive amount unless the simulator declines.</summary>
public abstract class SimulatedOnlinePayment : IPaymentStrategy
{
    private readonly IGatewaySimulator _gateway;

    protected SimulatedOnlinePayment(IGatewaySimulator gateway)
    {
        _gateway = gateway;
    }

    protected abstract string MethodName { get; }

    /// <summary>The customer type allowed to use this gateway (BR13).</summary>
    protected abstract string CustomerRole { get; }

    public bool IsAllowed(string customerRole, decimal total) => customerRole == CustomerRole;

    public PaymentResult Pay(decimal amount)
    {
        if (amount <= 0m)
            return new PaymentResult(false, "Failed", $"{MethodName} payment declined: invalid amount.");
        if (_gateway.DeclineAll)
            return new PaymentResult(false, "Failed", $"{MethodName} declined the payment (simulated gateway failure).");
        return new PaymentResult(true, PaymentStatus.Paid, $"{MethodName} payment approved (simulated).");
    }
}
