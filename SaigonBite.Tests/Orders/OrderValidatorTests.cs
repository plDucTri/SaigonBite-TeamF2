using SaigonBite.Models.Entities;
using SaigonBite.Services;

namespace SaigonBite.Tests.Orders;

/// <summary>EP/BVA tests of OrderValidator: BR1, BR2, BR8, BR12 (Test Cases sheet T-01, T-02, T-12, T-21 to T-24).</summary>
public class OrderValidatorTests
{
    private readonly OrderValidator _validator = new();
    private static readonly ISet<int> HubBrands = new HashSet<int> { 1 };

    private static MenuItem Item(int id, decimal price = 50_000m, int brandId = 1, bool available = true) => new MenuItem
    {
        ItemId = id, BrandId = brandId, Name = "Item " + id, Category = "c", Price = price,
        IsAvailable = available, IsActive = true
    };

    private static Dictionary<int, MenuItem> Items(int count, decimal price = 50_000m) =>
        Enumerable.Range(1, count).ToDictionary(i => i, i => Item(i, price));

    /// <summary>A valid order by default: one line, S 100,000, 1 km, load 50%.</summary>
    private List<RuleViolation> Check(
        decimal subtotal = 100_000m, decimal km = 1m, decimal load = 50m,
        IReadOnlyList<LineRequest>? lines = null, IReadOnlyDictionary<int, MenuItem>? items = null, bool isNew = true) =>
        _validator.Validate(new OrderCheck
        {
            Subtotal = subtotal,
            DistanceKm = km,
            HubLoadPct = load,
            Lines = lines ?? new[] { new LineRequest(1, 1) },
            Items = items ?? Items(1),
            HubBrandIds = HubBrands,
            IsNewOrder = isNew
        });

    // ---- T-01 (FR1, BR1) ----
    [Theory]
    [InlineData(49_999, false)]
    [InlineData(50_000, true)]
    [InlineData(50_001, true)]
    public void T01_MinimumSubtotal(int subtotal, bool accepted)
    {
        var errors = Check(subtotal: subtotal);
        Assert.Equal(accepted, !errors.Any(v => v.Rule == "BR1"));
    }

    // ---- T-02 (FR1, BR2) ----
    [Theory]
    [InlineData(7.99, true)]
    [InlineData(8.0, true)]
    [InlineData(8.01, false)]
    public void T02_Coverage(double km, bool accepted)
    {
        var errors = Check(km: (decimal)km);
        Assert.Equal(accepted, !errors.Any(v => v.Rule == "BR2"));
    }

    // ---- T-12 (FR4, BR8) ----
    [Theory]
    [InlineData(89, true)]
    [InlineData(90, false)]
    [InlineData(91, false)]
    public void T12_HubLoadProtection(int load, bool accepted)
    {
        var errors = Check(load: load);
        Assert.Equal(accepted, !errors.Any(v => v.Rule == "BR8"));
    }

    [Fact]
    public void T12_HubProtection_DoesNotApplyToAnEdit_A10()
    {
        Assert.DoesNotContain(Check(load: 95m, isNew: false), v => v.Rule == "BR8");
    }

    // ---- T-21 (FR1, BR12) quantity per line ----
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    [InlineData(-1, false)]
    public void T21_QuantityPerLine(int quantity, bool accepted)
    {
        var errors = Check(lines: new[] { new LineRequest(1, quantity) });
        Assert.Equal(accepted, !errors.Any(v => v.Rule == "BR12"));
    }

    // ---- T-22 (FR1, BR12) lines per order ----
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void T22_LinesPerOrder(int lineCount, bool accepted)
    {
        var lines = Enumerable.Range(1, lineCount).Select(i => new LineRequest(i, 1)).ToList();
        var errors = Check(lines: lines, items: Items(Math.Max(lineCount, 1)));
        Assert.Equal(accepted, !errors.Any(v => v.Rule == "BR12"));
    }

    // ---- T-23 (FR1, FR15, BR12) item not Available ----
    [Fact]
    public void T23_UnavailableItem_IsRejected()
    {
        var items = new Dictionary<int, MenuItem> { [1] = Item(1, available: false) };
        Assert.Contains(Check(items: items), v => v.Rule == "BR12" && v.Message.Contains("unavailable"));
    }

    // ---- T-24 (FR1, BR12) item from a brand the hub does not operate ----
    [Fact]
    public void T24_ItemFromAnotherHubsBrand_IsRejected()
    {
        var items = new Dictionary<int, MenuItem> { [1] = Item(1, brandId: 2) };
        Assert.Contains(Check(items: items), v => v.Message.Contains("not sold by this hub"));
    }

    [Fact]
    public void UnknownItem_IsRejected()
    {
        Assert.NotEmpty(Check(lines: new[] { new LineRequest(99, 1) }));
    }

    [Fact]
    public void ValidOrder_HasNoViolations()
    {
        Assert.Empty(Check());
    }

    [Fact]
    public void Merge_SameItemTwice_BecomesOneLine()
    {
        var merged = OrderValidator.Merge(new[] { new LineRequest(1, 2), new LineRequest(1, 3), new LineRequest(2, 1) });
        Assert.Equal(2, merged.Count);
        Assert.Equal(5, merged.Single(l => l.ItemId == 1).Quantity);
    }
}
