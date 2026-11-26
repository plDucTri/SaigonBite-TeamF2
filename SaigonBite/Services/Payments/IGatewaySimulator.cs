namespace SaigonBite.Services.Payments;

/// <summary>
/// The "mock gateway" of the Standard tiers (spec section G). An Admin can switch it to decline every online
/// payment, to demonstrate A-03 (a failed payment creates no order) and Figma D14.
/// </summary>
public interface IGatewaySimulator
{
    bool DeclineAll { get; }
    void SetDeclineAll(bool decline);
}

public class GatewaySimulator : IGatewaySimulator
{
    private volatile bool _declineAll;

    public bool DeclineAll => _declineAll;

    public void SetDeclineAll(bool decline) => _declineAll = decline;
}
