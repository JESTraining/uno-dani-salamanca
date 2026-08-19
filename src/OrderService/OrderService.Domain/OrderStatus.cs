namespace OrderService.Domain;

public enum OrderStatus
{
    Pending = 0,
    PaymentProcessing = 1,
    PaymentFailed = 2,
    InventoryProcessing = 3,
    InventoryFailed = 4,
    Completed = 5,
    Cancelled = 6,
    Shipped = 7,
    Delivered = 8
}
