using SaigonBite.Helpers;

namespace SaigonBite.Services.Payments;

/// <summary>Apple Pay, Foreign Tourists only (BR13).</summary>
public sealed class ApplePayPayment : SimulatedOnlinePayment
{
    public ApplePayPayment(IGatewaySimulator gateway) : base(gateway)
    {
    }

    protected override string MethodName => "Apple Pay";
    protected override string CustomerRole => Roles.Tourist;
}
