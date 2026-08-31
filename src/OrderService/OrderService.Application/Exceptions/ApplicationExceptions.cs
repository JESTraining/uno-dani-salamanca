namespace OrderService.Application.Exceptions;

public sealed class OrderNotFoundException : Exception
{
    public OrderNotFoundException(Guid orderId) : base($"Order '{orderId}' was not found.")
    {
    }
}

public sealed class InsufficientStockException : Exception
{
    public InsufficientStockException() : base("Insufficient stock for one or more order items.")
    {
    }
}

public sealed class InventoryServiceUnavailableException : Exception
{
    public InventoryServiceUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
