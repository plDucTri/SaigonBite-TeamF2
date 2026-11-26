using System.Globalization;

namespace SaigonBite.Helpers;

/// <summary>
/// Display formats for the order screens. They use the invariant culture on purpose, so an amount looks the same
/// in English and Vietnamese (205,000 VND) and a decimal never changes meaning when the language is switched.
/// </summary>
public static class MoneyFormat
{
    /// <summary>205000 becomes "205,000 VND".</summary>
    public static string Vnd(this decimal amount) =>
        amount.ToString("N0", CultureInfo.InvariantCulture) + " VND";

    public static string Pct(this decimal pct) =>
        pct.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    public static string When(this DateTime value) =>
        value.ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Reads a distance typed as 3.2 or 3,2 in any language. Returns false for text that is not a number.</summary>
    public static bool TryParseKm(string? text, out decimal km)
    {
        km = 0m;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out km) && km >= 0m;
    }
}
