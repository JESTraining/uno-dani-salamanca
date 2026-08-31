namespace OrderService.Domain;

public sealed class InvalidOrderOperationException : Exception
{
    public InvalidOrderOperationException(string message) : base(message)
    {
    }
}
