using SaigonBite.Services;
using SaigonBite.Validators;

namespace SaigonBite.Tests.Menu;

/// <summary>EP/BVA of the price of a menu item (FR15, T-50) and the length limits.</summary>
public class MenuValidatorTests
{
    private readonly MenuValidator _validator = new();

    private IReadOnlyList<string> Check(decimal? price, string name = "Pho Bo", string category = "Noodles", int brandId = 1) =>
        _validator.ValidateItem(new MenuItemChanges { Name = name, BrandId = brandId, Category = category, Price = price });

    [Theory]
    [InlineData(1)]            // just above 0
    [InlineData(50000)]
    [InlineData(99999999)]     // the maximum
    public void Valid_prices_are_accepted(int price) => Assert.Empty(Check(price));

    [Theory]
    [InlineData(0)]            // boundary: 0 is rejected
    [InlineData(-1)]
    [InlineData(100000000)]    // just above the maximum
    public void Invalid_prices_are_rejected(int price) => Assert.NotEmpty(Check(price));

    [Fact]
    public void A_price_with_decimals_or_no_price_is_rejected()
    {
        Assert.NotEmpty(Check(65000.5m));
        Assert.NotEmpty(Check(null));
        Assert.Empty(Check(65000.00m));   // 65,000.00 is a whole number
    }

    [Theory]
    [InlineData("noodles")]
    [InlineData("NOODLES")]
    [InlineData(" Noodles ")]
    public void The_category_list_ignores_letter_case_and_spaces(string category) => Assert.Empty(Check(50000, category: category));

    [Fact]
    public void Name_length_limit_is_100()
    {
        Assert.Empty(Check(50000, name: new string('a', 100)));
        Assert.NotEmpty(Check(50000, name: new string('a', 101)));
        Assert.Empty(_validator.ValidateBrand(new BrandChanges { Name = new string('a', 100) }));
        Assert.NotEmpty(_validator.ValidateBrand(new BrandChanges { Name = new string('a', 101) }));
    }
}
