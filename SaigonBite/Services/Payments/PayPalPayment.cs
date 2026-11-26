using SaigonBite.Helpers;

namespace SaigonBite.Services.Payments;

/// <summary>PayPal, Foreign Tourists only (BR13).</summary>
public sealed class PayPalPayment : SimulatedOnlinePayment
{
    public PayPalPayment(IGatewaySimulator gateway) : base(gateway)
    {
    }

    protected override string MethodName => "PayPal";
    protected override string CustomerRole => Roles.Tourist;
}
