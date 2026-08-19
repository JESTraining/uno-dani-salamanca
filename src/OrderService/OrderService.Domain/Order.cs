namespace OrderService.Domain;

public class Order
{
    private readonly List<OrderItem> _items = new();

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string CustomerName { get; private set; } = default!;
    public string CustomerEmail { get; private set; } = default!;
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order()
    {
    }

    public static Order Create(Guid customerId, string customerName, string customerEmail, IEnumerable<OrderItem> items)
    {
        if (customerId == Guid.Empty)
            throw new InvalidOrderOperationException("Customer id is required to create an order.");

        if (string.IsNullOrWhiteSpace(customerName))
            throw new InvalidOrderOperationException("Customer name is required to create an order.");

        if (string.IsNullOrWhiteSpace(customerEmail))
            throw new InvalidOrderOperationException("Customer email is required to create an order.");

        var itemList = items.ToList();
        if (itemList.Count == 0)
            throw new InvalidOrderOperationException("An order requires at least one item.");

        var totalAmount = itemList.Sum(i => i.Subtotal);
        if (totalAmount <= 0)
            throw new InvalidOrderOperationException("Order total amount must be greater than 0.");

        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            Status = OrderStatus.Pending,
            TotalAmount = totalAmount,
            CreatedAt = now,
            UpdatedAt = now
        };
        order._items.AddRange(itemList);

        return order;
    }

    /// Cancellation is only allowed while the order has not started shipping,
    /// per the business rule: cancellable only from Pending or PaymentProcessing.
    public void Cancel()
    {
        if (Status is not (OrderStatus.Pending or OrderStatus.PaymentProcessing))
            throw new InvalidOrderOperationException(
                $"Order in status '{Status}' cannot be cancelled. Only orders in 'Pending' or 'PaymentProcessing' can be cancelled.");

        Status = OrderStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    /// Shipped/Delivered orders are immutable, per the business rule.
    public void ChangeStatus(OrderStatus newStatus)
    {
        if (Status is OrderStatus.Shipped or OrderStatus.Delivered)
            throw new InvalidOrderOperationException(
                $"Order in status '{Status}' is immutable and cannot be modified.");

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }
}
