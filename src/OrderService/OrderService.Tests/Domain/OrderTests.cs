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

    // Saga transition methods (Phase 3). Each is idempotent: a no-op if
    // already at/past the target status, throws only for a genuinely
    // out-of-sequence transition.

    private static Order NewOrder() =>
        Order.Create(Guid.NewGuid(), "Jane Doe", "jane@example.com", new[] { ValidItem() });

    [Fact]
    public void StartPaymentProcessing_FromPending_SetsPaymentProcessing()
    {
        var order = NewOrder();

        order.StartPaymentProcessing();

        Assert.Equal(OrderStatus.PaymentProcessing, order.Status);
    }

    [Fact]
    public void StartPaymentProcessing_WhenAlreadyPaymentProcessing_IsNoOp()
    {
        var order = NewOrder();
        order.StartPaymentProcessing();

        order.StartPaymentProcessing();

        Assert.Equal(OrderStatus.PaymentProcessing, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.PaymentFailed)]
    [InlineData(OrderStatus.InventoryProcessing)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    public void StartPaymentProcessing_FromOtherStatus_Throws(OrderStatus initialStatus)
    {
        var order = NewOrder();
        order.ChangeStatus(initialStatus);

        Assert.Throws<InvalidOrderOperationException>(() => order.StartPaymentProcessing());
        Assert.Equal(initialStatus, order.Status);
    }

    [Fact]
    public void MarkPaymentProcessed_FromPaymentProcessing_SetsInventoryProcessing()
    {
        var order = NewOrder();
        order.StartPaymentProcessing();

        order.MarkPaymentProcessed();

        Assert.Equal(OrderStatus.InventoryProcessing, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.InventoryProcessing)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.InventoryFailed)]
    public void MarkPaymentProcessed_WhenAlreadyAdvanced_IsNoOp(OrderStatus advancedStatus)
    {
        var order = NewOrder();
        order.ChangeStatus(advancedStatus);

        order.MarkPaymentProcessed();

        Assert.Equal(advancedStatus, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.PaymentFailed)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    public void MarkPaymentProcessed_FromInvalidStatus_Throws(OrderStatus initialStatus)
    {
        var order = NewOrder();
        order.ChangeStatus(initialStatus);

        Assert.Throws<InvalidOrderOperationException>(() => order.MarkPaymentProcessed());
        Assert.Equal(initialStatus, order.Status);
    }

    [Fact]
    public void MarkPaymentFailed_FromPaymentProcessing_SetsPaymentFailed()
    {
        var order = NewOrder();
        order.StartPaymentProcessing();

        order.MarkPaymentFailed();

        Assert.Equal(OrderStatus.PaymentFailed, order.Status);
    }

    [Fact]
    public void MarkPaymentFailed_WhenAlreadyPaymentFailed_IsNoOp()
    {
        var order = NewOrder();
        order.StartPaymentProcessing();
        order.MarkPaymentFailed();

        order.MarkPaymentFailed();

        Assert.Equal(OrderStatus.PaymentFailed, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.InventoryProcessing)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public void MarkPaymentFailed_FromInvalidStatus_Throws(OrderStatus initialStatus)
    {
        var order = NewOrder();
        order.ChangeStatus(initialStatus);

        Assert.Throws<InvalidOrderOperationException>(() => order.MarkPaymentFailed());
        Assert.Equal(initialStatus, order.Status);
    }

    [Fact]
    public void Complete_FromInventoryProcessing_SetsCompleted()
    {
        var order = NewOrder();
        order.StartPaymentProcessing();
        order.MarkPaymentProcessed();

        order.Complete();

        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_IsNoOp()
    {
        var order = NewOrder();
        order.StartPaymentProcessing();
        order.MarkPaymentProcessed();
        order.Complete();

        order.Complete();

        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.PaymentProcessing)]
    [InlineData(OrderStatus.InventoryFailed)]
    [InlineData(OrderStatus.Cancelled)]
    public void Complete_FromInvalidStatus_Throws(OrderStatus initialStatus)
    {
        var order = NewOrder();
        order.ChangeStatus(initialStatus);

        Assert.Throws<InvalidOrderOperationException>(() => order.Complete());
        Assert.Equal(initialStatus, order.Status);
    }

    [Fact]
    public void MarkInventoryFailed_FromInventoryProcessing_SetsInventoryFailed()
    {
        var order = NewOrder();
        order.StartPaymentProcessing();
        order.MarkPaymentProcessed();

        order.MarkInventoryFailed();

        Assert.Equal(OrderStatus.InventoryFailed, order.Status);
    }

    [Fact]
    public void MarkInventoryFailed_WhenAlreadyInventoryFailed_IsNoOp()
    {
        var order = NewOrder();
        order.StartPaymentProcessing();
        order.MarkPaymentProcessed();
        order.MarkInventoryFailed();

        order.MarkInventoryFailed();

        Assert.Equal(OrderStatus.InventoryFailed, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.PaymentProcessing)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public void MarkInventoryFailed_FromInvalidStatus_Throws(OrderStatus initialStatus)
    {
        var order = NewOrder();
        order.ChangeStatus(initialStatus);

        Assert.Throws<InvalidOrderOperationException>(() => order.MarkInventoryFailed());
        Assert.Equal(initialStatus, order.Status);
    }
}
