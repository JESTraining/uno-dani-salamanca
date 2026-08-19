using OrderService.Domain;

namespace OrderService.Tests.Domain;

public class OrderItemTests
{
    [Fact]
    public void Constructor_WithValidData_ComputesSubtotal()
    {
        var item = new OrderItem(Guid.NewGuid(), "Widget", 3, 9.99m);

        Assert.Equal(29.97m, item.Subtotal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveQuantity_Throws(int quantity)
    {
        Assert.Throws<InvalidOrderOperationException>(() => new OrderItem(Guid.NewGuid(), "Widget", quantity, 9.99m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Constructor_WithNonPositiveUnitPrice_Throws(decimal unitPrice)
    {
        Assert.Throws<InvalidOrderOperationException>(() => new OrderItem(Guid.NewGuid(), "Widget", 1, unitPrice));
    }

    [Fact]
    public void Constructor_WithEmptyProductId_Throws()
    {
        Assert.Throws<InvalidOrderOperationException>(() => new OrderItem(Guid.Empty, "Widget", 1, 9.99m));
    }
}
