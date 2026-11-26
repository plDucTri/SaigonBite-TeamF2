using SaigonBite.Helpers;

namespace SaigonBite.Services.Payments;

/// <summary>VNPay, Local Customers only (BR13).</summary>
public sealed class VNPayPayment : SimulatedOnlinePayment
{
    public VNPayPayment(IGatewaySimulator gateway) : base(gateway)
    {
    }

    protected override string MethodName => "VNPay";
    protected override string CustomerRole => Roles.Local;
}
