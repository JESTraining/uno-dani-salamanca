namespace InventoryService.Application.Exceptions;

public sealed class ProductNotFoundException : Exception
{
    public ProductNotFoundException(Guid productId) : base($"Product '{productId}' was not found.")
    {
    }
}

/// Translated from EF Core's DbUpdateConcurrencyException by the
/// Infrastructure layer, so Application does not depend on EF Core types
/// directly and both the HTTP middleware and the internal reservation
/// retry loop can handle a single, provider-agnostic exception type.
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message)
    {
    }
}
