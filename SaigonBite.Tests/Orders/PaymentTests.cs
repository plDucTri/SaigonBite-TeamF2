using SaigonBite.Models.Entities;
using SaigonBite.Services;
using SaigonBite.Services.Payments;

namespace SaigonBite.Tests.Orders;

/// <summary>BR13, BR14 and the Strategy pattern (Test Cases sheet T-25 to T-28, T-56, T-57).</summary>
public class PaymentTests
{
    private static readonly string[] AllMethods = { "MoMo", "VNPay", "Apple Pay", "PayPal", "COD" };

    private static PaymentStrategyFactory NewFactory(GatewaySimulator? gateway = null) =>
        new PaymentStrategyFactory(gateway ?? new GatewaySimulator());

    private static PaymentService NewService(GatewaySimulator? gateway = null) => new PaymentService(NewFactory(gateway));

    // ---- T-25 (FR7, BR13) Local Customer ----
    [Theory]
    [InlineData("MoMo", true)]
    [InlineData("VNPay", true)]
    [InlineData("COD", true)]
    [InlineData("Apple Pay", false)]
    [InlineData("PayPal", false)]
    public void T25_LocalCustomer_PaymentMethods(string method, bool accepted)
    {
        var result = NewService().Process("Local", method, 100_000m);
        Assert.Equal(accepted, result.Success);
        if (!accepted) Assert.Equal("BR13", result.Violation!.Rule);
    }

    // ---- T-26 (FR7, BR13) Foreign Tourist ----
    [Theory]
    [InlineData("Apple Pay", true)]
    [InlineData("PayPal", true)]
    [InlineData("COD", true)]
    [InlineData("MoMo", false)]
    [InlineData("VNPay", false)]
    public void T26_ForeignTourist_PaymentMethods(string method, bool accepted)
    {
        var result = NewService().Process("Tourist", method, 100_000m);
        Assert.Equal(accepted, result.Success);
        if (!accepted) Assert.Equal("BR13", result.Violation!.Rule);
    }

    [Fact]
    public void Factory_ListsTheRightMethodsForEachCustomerType()
    {
        var factory = NewFactory();
        Assert.Equal(new[] { "COD", "MoMo", "VNPay" }, factory.MethodsFor("Local").OrderBy(x => x).ToArray());
        Assert.Equal(new[] { "Apple Pay", "COD", "PayPal" }, factory.MethodsFor("Tourist").OrderBy(x => x).ToArray());
        Assert.Empty(factory.MethodsFor("Kitchen"));
    }

    [Fact]
    public void Factory_IgnoresLetterCase_AndUnknownMethodIsNull()
    {
        var factory = NewFactory();
        Assert.NotNull(factory.Create("momo"));
        Assert.Equal("Apple Pay", factory.CanonicalName("apple pay"));
        Assert.Null(factory.Create("Bitcoin"));
    }

    [Fact]
    public void EveryMethod_IsAllowedForAtLeastOneCustomerType()
    {
        var factory = NewFactory();
        foreach (var m in AllMethods)
            Assert.True(factory.Create(m)!.IsAllowed("Local", 0m) || factory.Create(m)!.IsAllowed("Tourist", 0m), m);
    }

    // ---- T-27 (FR7, BR14) COD limit ----
    [Theory]
    [InlineData(999_000, true)]
    [InlineData(1_000_000, true)]
    [InlineData(1_001_000, false)]
    [InlineData(2_000_000, false)]
    public void T27_CodLimit(int total, bool allowed)
    {
        var cod = new CodPayment();
        Assert.Equal(allowed, cod.IsAllowed("Local", total));
        Assert.Equal(allowed, cod.IsAllowed("Tourist", total));
        Assert.Equal(allowed, cod.Pay(total).Success);

        var result = NewService().Process("Local", "COD", total);
        Assert.Equal(allowed, result.Success);
        if (!allowed) Assert.Equal("BR14", result.Violation!.Rule);
    }

    // ---- T-28 (FR6, FR7, BR11, BR14) the COD limit uses the ROUNDED total ----
    [Theory]
    [InlineData(1_000_499, 1_000_000, true)]    // rounds down to 1,000,000: COD accepted
    [InlineData(1_000_500, 1_001_000, false)]   // rounds up to 1,001,000: COD refused
    public void T28_CodLimitUsesRoundedTotal(int subtotal, int expectedTotal, bool codAllowed)
    {
        // S >= 300,000 so delivery is free; load 50% so there is no Quiet Kitchen discount
        var price = new PricingEngine().Calculate(new PricingRequest
        {
            Subtotal = subtotal, DistanceKm = 1m, HubLoadPct = 50m, PlacedAt = new DateTime(2026, 11, 24, 15, 0, 0)
        });
        Assert.Equal(expectedTotal, price.Total);
        Assert.Equal(codAllowed, new CodPayment().IsAllowed("Local", price.Total));
    }

    [Fact]
    public void Cod_StartsPending_OnlineStartsPaid()
    {
        Assert.Equal(PaymentStatus.Pending, new CodPayment().Pay(100_000m).Status);
        Assert.Equal(PaymentStatus.Paid, new MoMoPayment(new GatewaySimulator()).Pay(100_000m).Status);
    }

    [Fact]
    public void Options_DisableCodAboveTheLimit()
    {
        var options = NewService().GetOptions("Local", 1_270_000m);   // Figma D18
        Assert.False(options.Single(o => o.Method == "COD").Enabled);
        Assert.True(options.Single(o => o.Method == "MoMo").Enabled);
    }

    // ---- T-56 (FR7, BR13, A-03) the mock gateway is set to fail ----
    [Fact]
    public void T56_GatewayFailure_IsReportedAsFailedPayment()
    {
        var gateway = new GatewaySimulator();
        gateway.SetDeclineAll(true);

        var result = NewService(gateway).Process("Local", "MoMo", 100_000m);

        Assert.False(result.Success);
        Assert.True(result.GatewayFailed);
        Assert.Null(result.Violation);

        // Cash on Delivery has no online gateway, so it is not affected
        Assert.True(NewService(gateway).Process("Local", "COD", 100_000m).Success);
    }

    // ---- refund (BR16, A-02) ----
    [Fact]
    public void Refund_OnlinePayment_IsRefundedInFull()
    {
        var p = new Payment { Method = "MoMo", Amount = 100_000m, Status = PaymentStatus.Paid };
        NewService().Refund(p);
        Assert.True(p.Refunded);
        Assert.Equal(PaymentStatus.Refunded, p.Status);
    }

    [Fact]
    public void Refund_Cod_HasNothingToRefund()
    {
        var p = new Payment { Method = "COD", Amount = 100_000m, Status = PaymentStatus.Pending };
        NewService().Refund(p);
        Assert.False(p.Refunded);
        Assert.Equal(PaymentStatus.Voided, p.Status);
    }

    // ---- T-57 (NFR2) Change Request: a new payment method is ONE new class + ONE registration ----
    private sealed class ZaloPayPayment : IPaymentStrategy
    {
        public bool IsAllowed(string customerRole, decimal total) => customerRole == "Local";
        public PaymentResult Pay(decimal amount) => new PaymentResult(true, PaymentStatus.Paid, "ok");
    }

    [Fact]
    public void T57_NewPaymentMethod_NeedsOnlyANewClassAndARegistration()
    {
        var gateway = new GatewaySimulator();
        var factory = new PaymentStrategyFactory(new Dictionary<string, IPaymentStrategy>
        {
            ["MoMo"] = new MoMoPayment(gateway),
            ["VNPay"] = new VNPayPayment(gateway),
            ["Apple Pay"] = new ApplePayPayment(gateway),
            ["PayPal"] = new PayPalPayment(gateway),
            ["COD"] = new CodPayment(),
            ["ZaloPay"] = new ZaloPayPayment()          // the only new line
        });
        var service = new PaymentService(factory);

        Assert.True(service.Process("Local", "ZaloPay", 100_000m).Success);
        Assert.Equal("BR13", service.Process("Tourist", "ZaloPay", 100_000m).Violation!.Rule);
        Assert.Equal(4, factory.MethodsFor("Local").Count);   // the existing three plus the new one
    }
}
