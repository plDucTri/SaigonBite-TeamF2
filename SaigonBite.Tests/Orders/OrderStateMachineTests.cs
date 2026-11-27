using SaigonBite.Services;

namespace SaigonBite.Tests.Orders;

/// <summary>
/// BR15 at the table level (State Tests sheet). Cases that also need data (other hub, reason, shipper, cash)
/// are in OrderServiceTests: T-62, T-66, T-67, T-68.
/// </summary>
public class OrderStateMachineTests
{
    // ---- allowed transitions of section C (State Tests rows 1 to 8) ----
    [Theory]
    [InlineData(OrderStatus.Placed, OrderStatus.Confirmed, "Kitchen")]
    [InlineData(OrderStatus.Placed, OrderStatus.Cancelled, "Local")]
    [InlineData(OrderStatus.Placed, OrderStatus.Cancelled, "Tourist")]
    [InlineData(OrderStatus.Placed, OrderStatus.Cancelled, "Kitchen")]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Preparing, "Kitchen")]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled, "Local")]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled, "Admin")]
    [InlineData(OrderStatus.Preparing, OrderStatus.Ready, "Kitchen")]
    [InlineData(OrderStatus.Ready, OrderStatus.OutForDelivery, "Shipper")]
    [InlineData(OrderStatus.OutForDelivery, OrderStatus.Delivered, "Shipper")]
    [InlineData(OrderStatus.OutForDelivery, OrderStatus.Failed, "Shipper")]
    public void AllowedTransitions_AreAccepted(string from, string to, string role)
    {
        Assert.True(OrderStateMachine.Check(from, to, role).Allowed);
    }

    // ---- invalid transitions (FR8, BR15, BR16) ----
    [Theory]
    [InlineData(OrderStatus.Delivered, OrderStatus.Preparing, "Kitchen")]        // T-35
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed, "Kitchen")]        // T-36
    [InlineData(OrderStatus.Preparing, OrderStatus.Cancelled, "Local")]          // T-37 no cancellation after Preparing
    [InlineData(OrderStatus.Ready, OrderStatus.Delivered, "Shipper")]            // T-38 must go through Out for Delivery
    [InlineData(OrderStatus.Placed, OrderStatus.Ready, "Kitchen")]               // T-39
    [InlineData(OrderStatus.Placed, OrderStatus.Confirmed, "Local")]             // T-61 wrong actor
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled, "Kitchen")]        // T-63 only Customer or Admin
    [InlineData(OrderStatus.Failed, OrderStatus.Delivered, "Shipper")]           // T-64 terminal state
    [InlineData(OrderStatus.OutForDelivery, OrderStatus.Cancelled, "Local")]     // T-65
    [InlineData(OrderStatus.Placed, OrderStatus.Confirmed, "Shipper")]
    [InlineData(OrderStatus.Placed, OrderStatus.Cancelled, "Admin")]             // admin cancels only Confirmed
    [InlineData(OrderStatus.Confirmed, OrderStatus.Placed, "Kitchen")]           // backwards
    [InlineData(OrderStatus.Ready, OrderStatus.Preparing, "Kitchen")]            // backwards
    [InlineData(OrderStatus.Preparing, OrderStatus.Ready, "Shipper")]
    [InlineData(OrderStatus.Ready, OrderStatus.OutForDelivery, "Kitchen")]
    [InlineData(OrderStatus.OutForDelivery, OrderStatus.Delivered, "Local")]
    public void InvalidTransitions_AreRejected(string from, string to, string role)
    {
        var check = OrderStateMachine.Check(from, to, role);
        Assert.False(check.Allowed);
        Assert.StartsWith("BR15", check.Error);
    }

    [Fact]
    public void TerminalStates_AllowNothing()
    {
        foreach (var terminal in new[] { OrderStatus.Delivered, OrderStatus.Cancelled, OrderStatus.Failed })
            foreach (var to in OrderStatus.All)
                foreach (var role in new[] { "Local", "Tourist", "Kitchen", "Shipper", "Admin" })
                    Assert.False(OrderStateMachine.Check(terminal, to, role).Allowed);
    }

    [Fact]
    public void NextStatuses_ForCustomerAtPlaced_IsCancelOnly()
    {
        Assert.Equal(new[] { OrderStatus.Cancelled }, OrderStateMachine.NextStatuses(OrderStatus.Placed, "Local"));
    }
}
