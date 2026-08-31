namespace InventoryService.Domain;

public class Product
{
    public Guid Id { get; private set; }
    public string Sku { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int StockQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public int AvailableQuantity => StockQuantity - ReservedQuantity;

    private Product()
    {
    }

    public static Product Create(string sku, string name, string? description, decimal unitPrice, int stockQuantity)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new InvalidInventoryOperationException("SKU is required.");

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidInventoryOperationException("Product name is required.");

        if (unitPrice <= 0)
            throw new InvalidInventoryOperationException("Unit price must be greater than 0.");

        if (stockQuantity < 0)
            throw new InvalidInventoryOperationException("Stock quantity cannot be negative.");

        var now = DateTime.UtcNow;
        return new Product
        {
            Id = Guid.NewGuid(),
            Sku = sku,
            Name = name,
            Description = description,
            UnitPrice = unitPrice,
            StockQuantity = stockQuantity,
            ReservedQuantity = 0,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// Reserving beyond available stock is a protected invariant, not just
    /// an application-level check: the entity refuses it unconditionally,
    /// so overselling is structurally impossible regardless of caller.
    public void Reserve(int quantity)
    {
        if (quantity <= 0)
            throw new InvalidInventoryOperationException("Reservation quantity must be greater than 0.");

        if (quantity > AvailableQuantity)
            throw new InvalidInventoryOperationException(
                $"Cannot reserve {quantity} units of '{Sku}': only {AvailableQuantity} available.");

        ReservedQuantity += quantity;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReleaseReservation(int quantity)
    {
        if (quantity <= 0)
            throw new InvalidInventoryOperationException("Release quantity must be greater than 0.");

        if (quantity > ReservedQuantity)
            throw new InvalidInventoryOperationException($"Cannot release {quantity} units of '{Sku}': only {ReservedQuantity} are reserved.");

        ReservedQuantity -= quantity;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    /// Confirming permanently removes the quantity from stock (the sale is
    /// final) and clears it from the reserved count.
    public void ConfirmReservation(int quantity)
    {
        if (quantity <= 0)
            throw new InvalidInventoryOperationException("Confirmation quantity must be greater than 0.");

        if (quantity > ReservedQuantity)
            throw new InvalidInventoryOperationException($"Cannot confirm {quantity} units of '{Sku}': only {ReservedQuantity} are reserved.");

        ReservedQuantity -= quantity;
        StockQuantity -= quantity;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateStock(int newStockQuantity)
    {
        if (newStockQuantity < 0)
            throw new InvalidInventoryOperationException("Stock quantity cannot be negative.");

        if (newStockQuantity < ReservedQuantity)
            throw new InvalidInventoryOperationException(
                $"Cannot set stock to {newStockQuantity} for '{Sku}': {ReservedQuantity} units are currently reserved.");

        StockQuantity = newStockQuantity;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }
}
