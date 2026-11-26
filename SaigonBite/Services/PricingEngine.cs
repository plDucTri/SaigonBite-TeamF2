using SaigonBite.Models.Entities;

namespace SaigonBite.Services;

/// <summary>Everything the price calculation needs. The caller supplies the BR17 snapshot values.</summary>
public class PricingRequest
{
    /// <summary>Item subtotal S, before any discount (BR10 step 1).</summary>
    public decimal Subtotal { get; set; }
    public decimal DistanceKm { get; set; }
    /// <summary>Hub load in percent (0 to 100) at placement (BR7).</summary>
    public decimal HubLoadPct { get; set; }
    /// <summary>Placement timestamp, Vietnam local time (BR4, BR9 dates).</summary>
    public DateTime PlacedAt { get; set; }
    public bool HeavyRain { get; set; }
    /// <summary>The voucher the customer entered, or null. An invalid voucher is refused, not applied (T-13, T-16, T-17).</summary>
    public Voucher? Voucher { get; set; }
}

public class PricingResult
{
    public decimal Subtotal { get; set; }          // S
    public decimal QuietDiscount { get; set; }     // Q
    public decimal VoucherDiscount { get; set; }   // V
    public decimal FoodTotal { get; set; }         // F
    public decimal DeliveryFee { get; set; }       // D
    public decimal RushSurcharge { get; set; }     // R
    public decimal RainSurcharge { get; set; }     // R2
    public decimal RawTotal { get; set; }
    public decimal Total { get; set; }             // rounded once (BR11)
    public bool FreeDelivery { get; set; }         // BR6

    public bool VoucherApplied { get; set; }
    /// <summary>Why the voucher was not applied (BR9), or null. The order is still priced without it.</summary>
    public string? VoucherRefusedReason { get; set; }
}

public interface IPricingEngine
{
    /// <summary>Calculates S, Q, V, F, D, R, R2 and the rounded total in the BR10 order (BR3 to BR11).</summary>
    PricingResult Calculate(PricingRequest request);
}

/// <summary>
/// CRC card PricingEngine (report 2.4 and 2.5): BR3 to BR11. It does not reject orders (that is OrderValidator)
/// and it never touches the database, so the same price is produced on every path (place, edit, preview).
/// </summary>
public class PricingEngine : IPricingEngine
{
    public const decimal FreeDeliveryFrom = 300_000m;  // BR6
    public const decimal QuietBelowPct = 30m;          // BR7
    public const decimal QuietRate = 0.10m;            // BR7
    public const decimal RushRate = 0.20m;             // BR4
    public const decimal RainSurchargeAmount = 10_000m;// BR5
    public const decimal RoundingStep = 1_000m;        // BR11

    public PricingResult Calculate(PricingRequest r)
    {
        var res = new PricingResult { Subtotal = r.Subtotal };

        // BR10 step 2: Quiet Kitchen discount Q (BR7)
        res.QuietDiscount = r.HubLoadPct < QuietBelowPct ? r.Subtotal * QuietRate : 0m;

        // BR10 step 3: voucher V (BR9). A voucher that does not qualify is refused and the order goes on without it.
        if (r.Voucher != null)
        {
            string? refused = CheckVoucher(r.Voucher, r.Subtotal, r.PlacedAt);
            if (refused != null)
            {
                res.VoucherRefusedReason = refused;
            }
            else
            {
                res.VoucherApplied = true;
                res.VoucherDiscount = VoucherAmount(r.Voucher, r.Subtotal, res.QuietDiscount);
            }
        }

        // BR10 step 4: food total F
        res.FoodTotal = r.Subtotal - res.QuietDiscount - res.VoucherDiscount;

        // BR10 step 5: delivery fee D (BR3) and free-delivery waiver (BR6, tested against S)
        res.FreeDelivery = r.Subtotal >= FreeDeliveryFrom;
        res.DeliveryFee = res.FreeDelivery ? 0m : DeliveryFee(r.DistanceKm);

        // BR10 step 6: rush-hour surcharge R, only if D is not waived (BR4)
        res.RushSurcharge = (!res.FreeDelivery && IsRushHour(r.PlacedAt)) ? res.DeliveryFee * RushRate : 0m;

        // BR10 step 7: heavy-rain surcharge R2 still applies when delivery is free (BR5, BR6)
        res.RainSurcharge = r.HeavyRain ? RainSurchargeAmount : 0m;

        // BR10 steps 8 and 9: raw total, then round once (BR11)
        res.RawTotal = res.FoodTotal + res.DeliveryFee + res.RushSurcharge + res.RainSurcharge;
        res.Total = RoundTotal(res.RawTotal);
        return res;
    }

    /// <summary>BR3: 15,000 up to 2.0 km; then +5,000 per started km to 5.0 km; then 30,000 + 7,000 per started km.</summary>
    public static decimal DeliveryFee(decimal km)
    {
        if (km <= 2m) return 15_000m;
        if (km <= 5m) return 15_000m + 5_000m * Math.Ceiling(km - 2m);
        return 30_000m + 7_000m * Math.Ceiling(km - 5m);
    }

    /// <summary>BR4: [11:00, 13:00) and [17:30, 19:30). Start inclusive, end exclusive.</summary>
    public static bool IsRushHour(DateTime placedAt)
    {
        var t = placedAt.TimeOfDay;
        bool lunch = t >= new TimeSpan(11, 0, 0) && t < new TimeSpan(13, 0, 0);
        bool dinner = t >= new TimeSpan(17, 30, 0) && t < new TimeSpan(19, 30, 0);
        return lunch || dinner;
    }

    /// <summary>BR11: nearest 1,000, half rounds up. 64,950 becomes 65,000.</summary>
    public static decimal RoundTotal(decimal raw) =>
        Math.Round(raw / RoundingStep, 0, MidpointRounding.AwayFromZero) * RoundingStep;

    /// <summary>BR9: the voucher must be active, inside its dates (both days included) and S must reach its minimum subtotal.</summary>
    /// <returns>null if the voucher qualifies, otherwise the reason it is refused.</returns>
    public static string? CheckVoucher(Voucher v, decimal subtotal, DateTime placedAt)
    {
        if (!v.IsActive)
            return $"Voucher {v.Code} is not active.";

        var day = DateOnly.FromDateTime(placedAt);
        if (day < v.ValidFrom || day > v.ValidTo)
            return $"Voucher {v.Code} is not valid on this date.";

        if (subtotal < v.MinSubtotal)
            return $"Voucher {v.Code} needs a subtotal of at least {OrderValidator.Vnd(v.MinSubtotal)}.";

        return null;
    }

    /// <summary>
    /// BR9: PERCENT = p% of (S - Q) limited to MaxDiscount; FIXED = the amount, never more than (S - Q).
    /// Call only after CheckVoucher returned null.
    /// </summary>
    public static decimal VoucherAmount(Voucher v, decimal subtotal, decimal quiet)
    {
        decimal basis = subtotal - quiet;
        decimal amount;
        if (string.Equals(v.Type, "PERCENT", StringComparison.OrdinalIgnoreCase))
        {
            amount = v.Value / 100m * basis;
            if (v.MaxDiscount.HasValue && amount > v.MaxDiscount.Value)
                amount = v.MaxDiscount.Value;
        }
        else
        {
            amount = Math.Min(v.Value, basis);
        }
        return Math.Max(0m, amount);
    }
}
