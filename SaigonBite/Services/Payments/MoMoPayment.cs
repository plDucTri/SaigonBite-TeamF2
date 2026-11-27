using SaigonBite.Helpers;

namespace SaigonBite.Services.Payments;

/// <summary>MoMo e-wallet, Local Customers only (BR13).</summary>
public sealed class MoMoPayment : SimulatedOnlinePayment
{
    public MoMoPayment(IGatewaySimulator gateway) : base(gateway)
    {
    }

    protected override string MethodName => "MoMo";
    protected override string CustomerRole => Roles.Local;
}
