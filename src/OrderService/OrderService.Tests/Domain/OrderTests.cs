using OrderService.Domain;

namespace OrderService.Tests.Domain;

public class OrderTests
{
    private static OrderItem ValidItem() => new(Guid.NewGuid(), "Widget", 2, 9.99m);

    [Fact]
    public void Create_WithValidData_SetsStatusPendingAndComputesTotalFromItems()
    {
        var items = new[] { new OrderItem(Guid.NewGuid(), "Widget", 2, 10m), new OrderItem(Guid.NewGuid(), "Gadget", 1, 5m) };

        var order = Order.Create(Guid.NewGuid(), "Jane Doe", "jane@example.com", items);

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(25m, order.TotalAmount);
        Assert.Equal(2, order.Items.Count);
    }

    [Fact]
    public void Create_WithNoItems_Throws()
    {
        Assert.Throws<InvalidOrderOperationException>(() =>
            Order.Create(Guid.NewGuid(), "Jane Doe", "jane@example.com", Array.Empty<OrderItem>()));
    }

    [Fact]
    public void Create_WithEmptyCustomerId_Throws()
    {
        Assert.Throws<InvalidOrderOperationException>(() =>
            Order.Create(Guid.Empty, "Jane Doe", "jane@example.com", new[] { ValidItem() }));
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.PaymentProcessing)]
    public void Cancel_FromAllowedStatus_SetsStatusToCancelled(OrderStatus initialStatus)
    {
        var order = Order.Create(Guid.NewGuid(), "Jane Doe", "jane@example.com", new[] { ValidItem() });
        order.ChangeStatus(initialStatus);

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public void Cancel_FromDisallowedStatus_Throws(OrderStatus initialStatus)
    {
        var order = Order.Create(Guid.NewGuid(), "Jane Doe", "jane@example.com", new[] { ValidItem() });
        order.ChangeStatus(initialStatus);

        Assert.Throws<InvalidOrderOperationException>(() => order.Cancel());
        Assert.Equal(initialStatus, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    public void ChangeStatus_WhenCurrentlyImmutable_Throws(OrderStatus immutableStatus)
    {
        var order = Order.Create(Guid.NewGuid(), "Jane Doe", "jane@example.com", new[] { ValidItem() });
        order.ChangeStatus(immutableStatus);

        Assert.Throws<InvalidOrderOperationException>(() => order.ChangeStatus(OrderStatus.Cancelled));
    }

    [Fact]
    public void ChangeStatus_WhenNotImmutable_Succeeds()
    {
        var order = Order.Create(Guid.NewGuid(), "Jane Doe", "jane@example.com", new[] { ValidItem() });

        order.ChangeStatus(OrderStatus.PaymentProcessing);

        Assert.Equal(OrderStatus.PaymentProcessing, order.Status);
    }
}
