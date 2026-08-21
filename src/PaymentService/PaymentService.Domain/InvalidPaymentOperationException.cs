namespace PaymentService.Domain;

public sealed class InvalidPaymentOperationException : Exception
{
    public InvalidPaymentOperationException(string message) : base(message)
    {
    }
}
