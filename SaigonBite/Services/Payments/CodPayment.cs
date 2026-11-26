using SaigonBite.Helpers;

namespace SaigonBite.Services.Payments;

/// <summary>
/// Cash on Delivery for both customer types (BR13). Allowed only if the rounded total is 1,000,000 VND or less
/// (BR14). Nothing is collected at checkout: the payment starts as Pending and the shipper confirms the cash
/// before the order can become Delivered (OrderService.ConfirmCashAsync).
/// </summary>
public sealed class CodPayment : IPaymentStrategy
{
    public const decimal Limit = 1_000_000m;

    public bool IsAllowed(string customerRole, decimal total) =>
        (customerRole == Roles.Local || customerRole == Roles.Tourist) && total <= Limit;

    public PaymentResult Pay(decimal amount) =>
        amount > 0m && amount <= Limit
            ? new PaymentResult(true, PaymentStatus.Pending, "Cash on Delivery selected; the shipper collects the cash.")
            : new PaymentResult(false, "Failed", $"Cash on Delivery is allowed only up to {Limit:N0} VND.");
}
