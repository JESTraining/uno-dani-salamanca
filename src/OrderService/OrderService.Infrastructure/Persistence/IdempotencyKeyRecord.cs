namespace OrderService.Infrastructure.Persistence;

public class IdempotencyKeyRecord
{
    public string Key { get; set; } = default!;
    public Guid OrderId { get; set; }
    public DateTime CreatedAt { get; set; }
}
