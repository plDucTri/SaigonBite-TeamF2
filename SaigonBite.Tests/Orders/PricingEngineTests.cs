using SaigonBite.Models.Entities;
using SaigonBite.Services;

namespace SaigonBite.Tests.Orders;

/// <summary>
/// EP/BVA tests of PricingEngine (BR3 to BR11). Test IDs T-xx are the IDs of the Test Cases sheet.
/// "Std" = Local Customer, hub load 50% (no Quiet Kitchen discount), 1 km, placed 15:00, rain off,
/// no voucher: S 100,000 + D 15,000 = total 115,000.
/// </summary>
public class PricingEngineTests
{
    private static readonly DateTime Day = new DateTime(2026, 11, 24);
    private readonly PricingEngine _engine = new();

    private PricingResult Calc(
        decimal subtotal = 100_000m, decimal km = 1m, decimal load = 50m,
        string time = "15:00", bool rain = false, Voucher? voucher = null, DateTime? date = null) =>
        _engine.Calculate(new PricingRequest
        {
            Subtotal = subtotal,
            DistanceKm = km,
            HubLoadPct = load,
            PlacedAt = (date ?? Day).Date.Add(TimeSpan.Parse(time)),
            HeavyRain = rain,
            Voucher = voucher
        });

    // V10 of the seed script 03: PERCENT 10%, cap 30,000, minimum subtotal 100,000, valid 20/11 to 30/11
    private static Voucher V10() => new Voucher
    {
        Code = "V10", Type = "PERCENT", Value = 10, MaxDiscount = 30_000m, MinSubtotal = 100_000m,
        ValidFrom = new DateOnly(2026, 11, 20), ValidTo = new DateOnly(2026, 11, 30), IsActive = true
    };

    private static Voucher Fixed(decimal amount) => new Voucher
    {
        Code = "FIXED", Type = "FIXED", Value = amount, MaxDiscount = null, MinSubtotal = 0m,
        ValidFrom = new DateOnly(2026, 11, 20), ValidTo = new DateOnly(2026, 12, 31), IsActive = true
    };

    // ---- T-01 (FR1, BR1) totals around the minimum subtotal ----
    [Theory]
    [InlineData(50_000, 65_000)]
    [InlineData(50_001, 65_000)]   // raw 65,001
    public void T01_MinimumSubtotal_Totals(int subtotal, int expectedTotal)
    {
        Assert.Equal(expectedTotal, Calc(subtotal: subtotal).Total);
    }

    // ---- T-02 (FR1, BR2) totals at the edge of coverage; 8.01 is rejected by OrderValidator ----
    [Theory]
    [InlineData(7.99, 51_000, 151_000)]
    [InlineData(8.0, 51_000, 151_000)]
    public void T02_Coverage_Totals(double km, int fee, int total)
    {
        var r = Calc(km: (decimal)km);
        Assert.Equal(fee, r.DeliveryFee);
        Assert.Equal(total, r.Total);
    }

    // ---- T-03, T-04, T-05 (FR2, BR3) delivery fee bands ----
    [Theory]
    [InlineData(1.0, 15_000)]    // T-03 band 1
    [InlineData(2.0, 15_000)]
    [InlineData(2.01, 20_000)]   // T-04 band 2: 2.01 counts as 1 extra km
    [InlineData(3.0, 20_000)]
    [InlineData(3.01, 25_000)]
    [InlineData(5.0, 30_000)]
    [InlineData(5.01, 37_000)]   // T-05 band 3
    [InlineData(6.0, 37_000)]
    [InlineData(6.01, 44_000)]
    [InlineData(8.0, 51_000)]
    public void T03_T05_DeliveryFeeBands(double km, int expectedFee)
    {
        var r = Calc(km: (decimal)km);
        Assert.Equal(expectedFee, r.DeliveryFee);
        Assert.Equal(100_000 + expectedFee, r.Total);
    }

    // ---- T-06, T-07 (FR2, BR4) rush-hour windows: D = 15,000 so R = 3,000 ----
    [Theory]
    [InlineData("10:59", 0)]
    [InlineData("11:00", 3_000)]   // start inclusive
    [InlineData("12:59", 3_000)]
    [InlineData("13:00", 0)]       // end exclusive
    [InlineData("17:29", 0)]
    [InlineData("17:30", 3_000)]
    [InlineData("19:29", 3_000)]
    [InlineData("19:30", 0)]
    public void T06_T07_RushHourWindows(string time, int expectedRush)
    {
        var r = Calc(time: time);
        Assert.Equal(expectedRush, r.RushSurcharge);
        Assert.Equal(115_000 + expectedRush, r.Total);
    }

    // ---- T-08 (FR3, BR5) heavy rain ----
    [Theory]
    [InlineData(false, 0, 115_000)]
    [InlineData(true, 10_000, 125_000)]
    public void T08_HeavyRain(bool rain, int expectedR2, int expectedTotal)
    {
        var r = Calc(rain: rain);
        Assert.Equal(expectedR2, r.RainSurcharge);
        Assert.Equal(expectedTotal, r.Total);
    }

    // ---- T-09 (FR3, BR6) free-delivery threshold ----
    [Theory]
    [InlineData(299_999, 15_000, 315_000)]   // raw 314,999
    [InlineData(300_000, 0, 300_000)]
    [InlineData(300_001, 0, 300_000)]        // raw 300,001
    public void T09_FreeDeliveryThreshold(int subtotal, int fee, int total)
    {
        var r = Calc(subtotal: subtotal);
        Assert.Equal(fee, r.DeliveryFee);
        Assert.Equal(total, r.Total);
    }

    // ---- T-10 (FR3, BR5, BR6) free delivery waives D and R but not R2 ----
    [Theory]
    [InlineData(false, 300_000)]
    [InlineData(true, 310_000)]
    public void T10_FreeDelivery_WaivesFeeAndRush_NotRain(bool rain, int expectedTotal)
    {
        var r = Calc(subtotal: 300_000m, time: "12:00", rain: rain);   // 12:00 is a rush hour
        Assert.Equal(0m, r.DeliveryFee);
        Assert.Equal(0m, r.RushSurcharge);
        Assert.Equal(expectedTotal, r.Total);
    }

    // ---- T-11 (FR4, BR7) Quiet Kitchen threshold ----
    [Theory]
    [InlineData(29, 10_000, 105_000)]
    [InlineData(30, 0, 115_000)]       // exactly 30%: no discount
    [InlineData(31, 0, 115_000)]
    public void T11_QuietKitchenThreshold(int load, int expectedQ, int expectedTotal)
    {
        var r = Calc(load: load);
        Assert.Equal(expectedQ, r.QuietDiscount);
        Assert.Equal(expectedTotal, r.Total);
    }

    // ---- T-12 (FR4, BR8) price at 89%; the rejection at 90% is tested in OrderValidatorTests ----
    [Fact]
    public void T12_Load89_IsPricedNormally()
    {
        Assert.Equal(115_000m, Calc(load: 89m).Total);
    }

    // ---- T-13 (FR5, FR6, BR9, BR10) voucher minimum subtotal is checked against S ----
    [Theory]
    [InlineData(99_999, false, 115_000)]    // refused: raw 114,999
    [InlineData(100_000, true, 105_000)]
    [InlineData(100_001, true, 105_000)]    // V 10,000.1; raw 105,000.9
    public void T13_VoucherMinimumSubtotal(int subtotal, bool applied, int expectedTotal)
    {
        var r = Calc(subtotal: subtotal, voucher: V10());
        Assert.Equal(applied, r.VoucherApplied);
        Assert.Equal(applied, r.VoucherRefusedReason == null);
        Assert.Equal(expectedTotal, r.Total);
    }

    [Fact]
    public void T13_VoucherIsTakenFromSubtotalMinusQuiet_ButMinimumIsCheckedAgainstS()
    {
        var r = Calc(subtotal: 100_000m, load: 20m, voucher: V10());   // S - Q = 90,000 is below 100,000 but S is not
        Assert.True(r.VoucherApplied);
        Assert.Equal(10_000m, r.QuietDiscount);
        Assert.Equal(9_000m, r.VoucherDiscount);
        Assert.Equal(96_000m, r.Total);
    }

    // ---- T-14 (FR5, BR9) percentage cap ----
    [Theory]
    [InlineData(299_990, 29_999, 285_000)]   // fee 15,000 still applies; raw 284,991
    [InlineData(300_000, 30_000, 270_000)]   // fee waived
    [InlineData(300_010, 30_000, 270_000)]   // capped; raw 270,010
    public void T14_VoucherPercentageCap(int subtotal, int expectedV, int expectedTotal)
    {
        var r = Calc(subtotal: subtotal, voucher: V10());
        Assert.Equal(expectedV, r.VoucherDiscount);
        Assert.Equal(expectedTotal, r.Total);
    }

    // ---- T-15 (FR5, BR9) FIXED voucher limited to S - Q ----
    [Theory]
    [InlineData(59_999, 59_999)]   // raw 15,001
    [InlineData(60_000, 60_000)]
    [InlineData(70_000, 60_000)]   // limited to S - Q
    public void T15_FixedVoucherLimitedToSubtotalMinusQuiet(int fixedAmount, int expectedV)
    {
        var r = Calc(subtotal: 60_000m, voucher: Fixed(fixedAmount));
        Assert.Equal(expectedV, r.VoucherDiscount);
        Assert.Equal(15_000m, r.Total);
    }

    // ---- T-16 (FR5, BR9) validity dates, both days included ----
    [Theory]
    [InlineData("2026-11-19", false, 115_000)]
    [InlineData("2026-11-20", true, 105_000)]
    [InlineData("2026-11-30", true, 105_000)]
    [InlineData("2026-12-01", false, 115_000)]
    public void T16_VoucherValidityDates(string date, bool applied, int expectedTotal)
    {
        var r = Calc(voucher: V10(), date: DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(applied, r.VoucherApplied);
        Assert.Equal(expectedTotal, r.Total);
    }

    // ---- T-17 (FR5, BR9) (a) inactive voucher ----
    [Fact]
    public void T17a_InactiveVoucher_IsRefused()
    {
        var v = V10();
        v.IsActive = false;
        var r = Calc(voucher: v);
        Assert.False(r.VoucherApplied);
        Assert.NotNull(r.VoucherRefusedReason);
        Assert.Equal(115_000m, r.Total);
    }

    // ---- T-18 (FR2, FR4, FR6, FR7, BR3, BR7, BR10) specification worked example ----
    [Fact]
    public void T18_WorkedExample_FromSpecification()
    {
        var r = Calc(subtotal: 200_000m, km: 3.2m, load: 20m);
        Assert.Equal(20_000m, r.QuietDiscount);
        Assert.Equal(0m, r.VoucherDiscount);
        Assert.Equal(180_000m, r.FoodTotal);
        Assert.Equal(25_000m, r.DeliveryFee);
        Assert.Equal(0m, r.RushSurcharge);
        Assert.Equal(0m, r.RainSurcharge);
        Assert.Equal(205_000m, r.RawTotal);
        Assert.Equal(205_000m, r.Total);
    }

    // ---- T-19 (FR2 to FR6) every price component at once ----
    [Fact]
    public void T19_EveryComponent_InTheBR10Order()
    {
        var r = Calc(subtotal: 210_000m, km: 3.2m, load: 20m, time: "12:00", rain: true, voucher: V10());
        Assert.Equal(21_000m, r.QuietDiscount);
        Assert.Equal(18_900m, r.VoucherDiscount);   // 10% of S - Q = 189,000
        Assert.Equal(170_100m, r.FoodTotal);
        Assert.Equal(25_000m, r.DeliveryFee);
        Assert.Equal(5_000m, r.RushSurcharge);
        Assert.Equal(10_000m, r.RainSurcharge);
        Assert.Equal(210_100m, r.RawTotal);
        Assert.Equal(210_000m, r.Total);
    }

    // ---- T-20 (FR6, BR11) rounding half up, once at the end ----
    [Theory]
    [InlineData(50_499, 65_000)]   // raw 65,499
    [InlineData(50_500, 66_000)]   // raw 65,500
    [InlineData(50_501, 66_000)]   // raw 65,501
    [InlineData(49_950, 65_000)]   // raw 64,950: the example of BR11
    public void T20_RoundingHalfUp(int subtotal, int expectedTotal)
    {
        var r = Calc(subtotal: subtotal);
        Assert.Equal(expectedTotal, r.Total);
        Assert.Equal(0m, r.Total % 1000m);
    }
}
