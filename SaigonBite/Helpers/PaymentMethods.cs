namespace SaigonBite.Helpers;

/// <summary>
/// Payment method names stored in Payments.Method (same spelling as the CHECK constraint in the database).
/// All is the order used on screen, for example in Revenue Report R1.
/// </summary>
public static class PaymentMethods
{
    public const string MoMo = "MoMo";
    public const string VNPay = "VNPay";
    public const string ApplePay = "Apple Pay";
    public const string PayPal = "PayPal";
    public const string Cod = "COD";

    public static readonly string[] All = { MoMo, VNPay, ApplePay, PayPal, Cod };
}
