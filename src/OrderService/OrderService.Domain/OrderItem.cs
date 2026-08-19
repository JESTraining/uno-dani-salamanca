namespace OrderService.Domain;

public class OrderItem
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    public decimal Subtotal => Quantity * UnitPrice;

    private OrderItem()
    {
    }

    public OrderItem(Guid productId, string productName, int quantity, decimal unitPrice)
    {
        if (productId == Guid.Empty)
            throw new InvalidOrderOperationException("Product id is required for an order item.");

        if (string.IsNullOrWhiteSpace(productName))
            throw new InvalidOrderOperationException("Product name is required for an order item.");

        if (quantity <= 0)
            throw new InvalidOrderOperationException("Order item quantity must be greater than 0.");

        if (unitPrice <= 0)
            throw new InvalidOrderOperationException("Order item unit price must be greater than 0.");

        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
