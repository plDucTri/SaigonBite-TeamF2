using SaigonBite.Helpers;
using SaigonBite.Services;

namespace SaigonBite.Tests.Services;

/// <summary>Revenue Report R1 (FR13, test T-45). Real ReportService and OrderRepository on EF InMemory.</summary>
public class ReportServiceTests
{
    private const string Local = "0901234567";   // seeded Local customer
    private const string Tourist = "DEMO12345";  // seeded Foreign Tourist

    private static readonly DateOnly FirstDay = new(2026, 11, 1);
    private static readonly DateOnly LastDay = new(2026, 11, 13);
    private static readonly DateTime InRange = new(2026, 11, 10, 12, 0, 0);

    private static RevenueReportData Run(RegistrationTestSetup setup, DateOnly fromDate, DateOnly toDate)
    {
        var result = ReportTestData.NewReport(setup).RevenueReport(fromDate, toDate);
        Assert.True(result.Ok);
        return result.Value!;
    }

    private static decimal Revenue(IReadOnlyList<RevenueRow> rows, string name) => rows.Single(r => r.Name == name).Revenue;

    // Both tables must always add up to the included revenue.
    private static void AssertTablesAddUp(RevenueReportData data)
    {
        Assert.Equal(data.IncludedRevenue, data.ByCategory.Sum(r => r.Revenue));
        Assert.Equal(data.IncludedRevenue, data.ByPaymentMethod.Sum(r => r.Revenue));
    }

    // ---------- T-45 ----------

    [Fact]
    public void T45_counts_Delivered_orders_and_leaves_out_Cancelled_and_Failed()
    {
        using var setup = AdminTestData.NewSetup();
        var noodles = ReportTestData.AddItem(setup, "Noodles", 50000);
        var drinks = ReportTestData.AddItem(setup, "Drinks", 30000);
        var rice = ReportTestData.AddItem(setup, "Rice", 40000);

        // The three Delivered orders of T-45 (totals include fees, so they are bigger than the lines).
        ReportTestData.AddOrder(setup, Local, OrderStatuses.Delivered, PaymentMethods.MoMo, InRange, 115000, (noodles, 2));
        ReportTestData.AddOrder(setup, Tourist, OrderStatuses.Delivered, PaymentMethods.PayPal, InRange, 105000, (drinks, 3));
        ReportTestData.AddOrder(setup, Local, OrderStatuses.Delivered, PaymentMethods.Cod, InRange, 95000, (rice, 2));
        // One Cancelled and one Failed order: they must not be counted.
        ReportTestData.AddOrder(setup, Local, OrderStatuses.Cancelled, PaymentMethods.MoMo, InRange, 200000, (noodles, 4));
        ReportTestData.AddOrder(setup, Tourist, OrderStatuses.Failed, PaymentMethods.Cod, InRange, 150000, (rice, 3));

        RevenueReportData data = Run(setup, FirstDay, LastDay);

        Assert.Equal(315000m, data.IncludedRevenue); // 115,000 + 105,000 + 95,000
        Assert.Equal(3, data.OrderCount);

        Assert.Equal(3, data.ByCategory.Count);
        Assert.Equal(115000m, Revenue(data.ByCategory, "Noodles"));
        Assert.Equal(105000m, Revenue(data.ByCategory, "Drinks"));
        Assert.Equal(95000m, Revenue(data.ByCategory, "Rice"));

        Assert.Equal(115000m, Revenue(data.ByPaymentMethod, PaymentMethods.MoMo));
        Assert.Equal(105000m, Revenue(data.ByPaymentMethod, PaymentMethods.PayPal));
        Assert.Equal(95000m, Revenue(data.ByPaymentMethod, PaymentMethods.Cod));
        Assert.Equal(0m, Revenue(data.ByPaymentMethod, PaymentMethods.VNPay));
        Assert.Equal(0m, Revenue(data.ByPaymentMethod, PaymentMethods.ApplePay));

        AssertTablesAddUp(data);
    }

    [Theory]
    [InlineData(OrderStatuses.Placed, true)]
    [InlineData(OrderStatuses.Confirmed, true)]
    [InlineData(OrderStatuses.Preparing, true)]
    [InlineData(OrderStatuses.Ready, true)]
    [InlineData(OrderStatuses.OutForDelivery, true)]
    [InlineData(OrderStatuses.Delivered, true)]
    [InlineData(OrderStatuses.Cancelled, false)]
    [InlineData(OrderStatuses.Failed, false)]
    public void Every_status_is_counted_except_Cancelled_and_Failed(string status, bool counted)
    {
        using var setup = AdminTestData.NewSetup();
        var noodles = ReportTestData.AddItem(setup, "Noodles", 50000);
        ReportTestData.AddOrder(setup, Local, status, PaymentMethods.MoMo, InRange, 65000, (noodles, 1));

        RevenueReportData data = Run(setup, FirstDay, LastDay);

        Assert.Equal(counted ? 65000m : 0m, data.IncludedRevenue);
        Assert.Equal(counted ? 1 : 0, data.OrderCount);
    }

    // ---------- revenue by category ----------

    [Fact]
    public void An_order_with_two_categories_is_shared_by_line_amounts()
    {
        using var setup = AdminTestData.NewSetup();
        var noodles = ReportTestData.AddItem(setup, "Noodles", 60000);
        var drinks = ReportTestData.AddItem(setup, "Drinks", 20000);

        // Lines: noodles 60,000 and drinks 20,000 (75% and 25%). The Total is 92,000.
        ReportTestData.AddOrder(setup, Local, OrderStatuses.Delivered, PaymentMethods.MoMo, InRange, 92000, (noodles, 1), (drinks, 1));

        RevenueReportData data = Run(setup, FirstDay, LastDay);

        Assert.Equal(69000m, Revenue(data.ByCategory, "Noodles"));
        Assert.Equal(23000m, Revenue(data.ByCategory, "Drinks"));
        Assert.Equal(92000m, Revenue(data.ByPaymentMethod, PaymentMethods.MoMo));
        AssertTablesAddUp(data);
    }

    [Fact]
    public void Category_rows_add_up_exactly_after_rounding()
    {
        using var setup = AdminTestData.NewSetup();
        var noodles = ReportTestData.AddItem(setup, "Noodles", 50000);
        var drinks = ReportTestData.AddItem(setup, "Drinks", 30000);
        var rice = ReportTestData.AddItem(setup, "Rice", 20000);

        // Exact shares are 50,000.5 / 30,000.3 / 20,000.2. The one VND left goes to the biggest fraction (Noodles).
        ReportTestData.AddOrder(setup, Local, OrderStatuses.Delivered, PaymentMethods.MoMo, InRange, 100001, (noodles, 1), (drinks, 1), (rice, 1));

        RevenueReportData data = Run(setup, FirstDay, LastDay);

        Assert.Equal(50001m, Revenue(data.ByCategory, "Noodles"));
        Assert.Equal(30000m, Revenue(data.ByCategory, "Drinks"));
        Assert.Equal(20000m, Revenue(data.ByCategory, "Rice"));
        Assert.Equal(100001m, data.ByCategory.Sum(r => r.Revenue));
    }

    [Fact]
    public void Equal_fractions_are_settled_by_category_name()
    {
        using var setup = AdminTestData.NewSetup();
        var rice = ReportTestData.AddItem(setup, "Rice", 1000);
        var noodles = ReportTestData.AddItem(setup, "Noodles", 1000);
        var drinks = ReportTestData.AddItem(setup, "Drinks", 1000);

        // 100 shared by three equal lines: 33.33 each, the extra VND goes to the first name (Drinks).
        ReportTestData.AddOrder(setup, Local, OrderStatuses.Delivered, PaymentMethods.MoMo, InRange, 100, (rice, 1), (noodles, 1), (drinks, 1));

        RevenueReportData data = Run(setup, FirstDay, LastDay);

        Assert.Equal(34m, Revenue(data.ByCategory, "Drinks"));
        Assert.Equal(33m, Revenue(data.ByCategory, "Noodles"));
        Assert.Equal(33m, Revenue(data.ByCategory, "Rice"));
    }

    [Fact]
    public void Same_category_in_many_orders_is_one_row_and_the_biggest_comes_first()
    {
        using var setup = AdminTestData.NewSetup();
        var noodles = ReportTestData.AddItem(setup, "Noodles", 50000);
        var noodlesLower = ReportTestData.AddItem(setup, "noodles", 50000); // same category, other spelling
        var drinks = ReportTestData.AddItem(setup, "Drinks", 30000);
        ReportTestData.AddOrder(setup, Local, OrderStatuses.Delivered, PaymentMethods.MoMo, InRange, 60000, (noodles, 1));
        ReportTestData.AddOrder(setup, Tourist, OrderStatuses.Delivered, PaymentMethods.PayPal, InRange, 60000, (noodlesLower, 1));
        ReportTestData.AddOrder(setup, Local, OrderStatuses.Delivered, PaymentMethods.Cod, InRange, 40000, (drinks, 1));

        RevenueReportData data = Run(setup, FirstDay, LastDay);

        Assert.Equal(2, data.ByCategory.Count);
        Assert.Equal("noodles", data.ByCategory[0].Name, ignoreCase: true);
        Assert.Equal(120000m, data.ByCategory[0].Revenue);
        Assert.Equal("Drinks", data.ByCategory[1].Name);
    }

    [Fact]
    public void Orders_of_inactive_items_and_brands_are_still_counted()
    {
        using var setup = AdminTestData.NewSetup();
        var oldItem = ReportTestData.AddItem(setup, "Sandwiches", 40000, active: false);
        ReportTestData.AddOrder(setup, Local, OrderStatuses.Delivered, PaymentMethods.VNPay, InRange, 55000, (oldItem, 1));

        RevenueReportData data = Run(setup, FirstDay, LastDay);

        Assert.Equal(55000m, data.IncludedRevenue);
        Assert.Equal(55000m, Revenue(data.ByCategory, "Sandwiches"));
    }

    // ---------- date range ----------

    [Fact]
    public void The_range_starts_at_midnight_of_the_first_day_and_ends_at_the_end_of_the_last_day()
    {
        using var setup = AdminTestData.NewSetup();
        var noodles = ReportTestData.AddItem(setup, "Noodles", 50000);
        ReportTestData.AddOrder(setup, Local, OrderStatuses.Delivered, PaymentMethods.MoMo, new DateTime(2026, 10, 31, 23, 59, 59), 1000, (noodles, 1));  // before
        ReportTestData.AddOrder(setup, Local, OrderStatuses.Delivered, PaymentMethods.MoMo, new DateTime(2026, 11, 1, 0, 0, 0), 2000, (noodles, 1));      // first moment
        ReportTestData.AddOrder(setup, Local, OrderStatuses.Delivered, PaymentMethods.MoMo, new DateTime(2026, 11, 13, 23, 59, 59), 4000, (noodles, 1));   // last day, late
        ReportTestData.AddOrder(setup, Local, OrderStatuses.Delivered, PaymentMethods.MoMo, new DateTime(2026, 11, 14, 0, 0, 0), 8000, (noodles, 1));      // after

        RevenueReportData data = Run(setup, FirstDay, LastDay);

        Assert.Equal(6000m, data.IncludedRevenue); // 2,000 + 4,000
        Assert.Equal(2, data.OrderCount);
    }

    [Fact]
    public void One_day_range_works()
    {
        using var setup = AdminTestData.NewSetup();
        var noodles = ReportTestData.AddItem(setup, "Noodles", 50000);
        ReportTestData.AddOrder(setup, Local, OrderStatuses.Delivered, PaymentMethods.MoMo, InRange, 65000, (noodles, 1));

        RevenueReportData data = Run(setup, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 10));

        Assert.Equal(65000m, data.IncludedRevenue);
    }

    [Fact]
    public void Range_with_no_orders_shows_zero()
    {
        using var setup = AdminTestData.NewSetup();

        RevenueReportData data = Run(setup, FirstDay, LastDay);

        Assert.Equal(0m, data.IncludedRevenue);
        Assert.Equal(0, data.OrderCount);
        Assert.Empty(data.ByCategory);
        Assert.Equal(PaymentMethods.All.Length, data.ByPaymentMethod.Count); // all five methods, each 0
        Assert.All(data.ByPaymentMethod, row => Assert.Equal(0m, row.Revenue));
    }

    [Fact]
    public void End_date_before_start_date_is_rejected()
    {
        using var setup = AdminTestData.NewSetup();

        var result = ReportTestData.NewReport(setup).RevenueReport(LastDay, FirstDay);

        Assert.False(result.Ok);
        Assert.Contains("end date", result.Errors[0]);
    }

    [Fact]
    public void Payment_methods_are_listed_in_the_fixed_order()
    {
        using var setup = AdminTestData.NewSetup();

        RevenueReportData data = Run(setup, FirstDay, LastDay);

        Assert.Equal(new[] { "MoMo", "VNPay", "Apple Pay", "PayPal", "COD" }, data.ByPaymentMethod.Select(r => r.Name).ToArray());
    }

    // ---------- FR14: a deactivated customer does not hide orders from the report ----------

    [Fact]
    public void Orders_of_a_deactivated_customer_are_still_counted()
    {
        using var setup = AdminTestData.NewSetup();
        var noodles = ReportTestData.AddItem(setup, "Noodles", 50000);
        ReportTestData.AddOrder(setup, Local, OrderStatuses.Delivered, PaymentMethods.MoMo, InRange, 65000, (noodles, 1));
        int adminId = AdminTestData.Find(setup, "ADMIN").UserId;
        setup.Service.DeactivateAccount(AdminTestData.Find(setup, Local).UserId, adminId);

        RevenueReportData data = Run(setup, FirstDay, LastDay);

        Assert.Equal(65000m, data.IncludedRevenue);
    }
}
