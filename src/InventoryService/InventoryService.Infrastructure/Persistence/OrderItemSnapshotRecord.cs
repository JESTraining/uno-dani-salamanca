namespace InventoryService.Infrastructure.Persistence;

public class OrderItemSnapshotRecord
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
}
