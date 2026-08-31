namespace InventoryService.Domain;

public class InventoryReservation
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTime ReservedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? ReleasedAt { get; private set; }

    private InventoryReservation()
    {
    }

    public static InventoryReservation Create(Guid orderId, Guid productId, int quantity, TimeSpan expiresIn)
    {
        if (orderId == Guid.Empty)
            throw new InvalidInventoryOperationException("Order id is required to create a reservation.");

        if (productId == Guid.Empty)
            throw new InvalidInventoryOperationException("Product id is required to create a reservation.");

        if (quantity <= 0)
            throw new InvalidInventoryOperationException("Reservation quantity must be greater than 0.");

        var now = DateTime.UtcNow;
        return new InventoryReservation
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            Quantity = quantity,
            Status = ReservationStatus.Reserved,
            ReservedAt = now,
            ExpiresAt = now.Add(expiresIn)
        };
    }

    public void Confirm()
    {
        if (Status != ReservationStatus.Reserved)
            throw new InvalidInventoryOperationException($"Reservation in status '{Status}' cannot be confirmed.");

        Status = ReservationStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
    }

    public void Release()
    {
        if (Status != ReservationStatus.Reserved)
            throw new InvalidInventoryOperationException($"Reservation in status '{Status}' cannot be released.");

        Status = ReservationStatus.Released;
        ReleasedAt = DateTime.UtcNow;
    }

    /// Distinct from Release(): this is specifically the automatic outcome
    /// of the 5-minute timeout rule, so it is traceable separately from a
    /// deliberate release.
    public void Expire()
    {
        if (Status != ReservationStatus.Reserved)
            throw new InvalidInventoryOperationException($"Reservation in status '{Status}' cannot expire.");

        Status = ReservationStatus.Expired;
        ReleasedAt = DateTime.UtcNow;
    }

    public bool HasExpired(DateTime asOf) => Status == ReservationStatus.Reserved && ExpiresAt <= asOf;
}
