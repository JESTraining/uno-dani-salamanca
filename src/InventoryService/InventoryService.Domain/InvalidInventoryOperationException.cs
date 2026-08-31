namespace InventoryService.Domain;

public sealed class InvalidInventoryOperationException : Exception
{
    public InvalidInventoryOperationException(string message) : base(message)
    {
    }
}
