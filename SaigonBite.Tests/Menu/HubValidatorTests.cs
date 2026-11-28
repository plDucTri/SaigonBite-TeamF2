using SaigonBite.Services;
using SaigonBite.Validators;

namespace SaigonBite.Tests.Menu;

/// <summary>EP/BVA of the kitchen capacity and the length limits of a hub (FR17).</summary>
public class HubValidatorTests
{
    private readonly HubValidator _validator = new();

    private IReadOnlyList<string> Check(int? capacity, string name = "Hub X", string address = "1 Le Loi, District 1") =>
        _validator.ValidateHub(new HubChanges { Name = name, Address = address, KitchenCapacity = capacity });

    [Theory]
    [InlineData(1)]       // minimum
    [InlineData(20)]
    [InlineData(1000)]    // maximum
    public void Valid_capacities_are_accepted(int capacity) => Assert.Empty(Check(capacity));

    [Theory]
    [InlineData(0)]       // just below the minimum
    [InlineData(-5)]
    [InlineData(1001)]    // just above the maximum
    public void Invalid_capacities_are_rejected(int capacity) => Assert.NotEmpty(Check(capacity));

    [Fact]
    public void A_missing_capacity_name_or_address_is_rejected()
    {
        Assert.NotEmpty(Check(null));
        Assert.NotEmpty(Check(10, name: "  "));
        Assert.NotEmpty(Check(10, address: ""));
    }

    [Fact]
    public void Name_and_address_length_limits_are_100_and_200()
    {
        Assert.Empty(Check(10, name: new string('a', 100), address: new string('b', 200)));
        Assert.NotEmpty(Check(10, name: new string('a', 101)));
        Assert.NotEmpty(Check(10, address: new string('b', 201)));
    }
}
