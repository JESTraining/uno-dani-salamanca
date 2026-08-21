using PaymentService.Domain;

namespace PaymentService.Tests.Domain;

public class PaymentTests
{
    [Fact]
    public void Create_WithValidData_SetsStatusPending()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "CreditCard");

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(100m, payment.Amount);
    }

    [Fact]
    public void Create_WithEmptyOrderId_Throws()
    {
        Assert.Throws<InvalidPaymentOperationException>(() => Payment.Create(Guid.Empty, 100m, "CreditCard"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_WithNonPositiveAmount_Throws(decimal amount)
    {
        Assert.Throws<InvalidPaymentOperationException>(() => Payment.Create(Guid.NewGuid(), amount, "CreditCard"));
    }

    [Fact]
    public void MarkAsProcessing_FromPending_Succeeds()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "CreditCard");

        payment.MarkAsProcessing();

        Assert.Equal(PaymentStatus.Processing, payment.Status);
    }

    [Fact]
    public void MarkAsProcessing_WhenNotPending_Throws()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "CreditCard");
        payment.MarkAsProcessing();

        Assert.Throws<InvalidPaymentOperationException>(() => payment.MarkAsProcessing());
    }

    [Fact]
    public void MarkAsSucceeded_FromProcessing_SetsTransactionId()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "CreditCard");
        payment.MarkAsProcessing();
        var transactionId = Guid.NewGuid();

        payment.MarkAsSucceeded(transactionId);

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(transactionId, payment.TransactionId);
    }

    [Fact]
    public void MarkAsSucceeded_WhenNotProcessing_Throws()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "CreditCard");

        Assert.Throws<InvalidPaymentOperationException>(() => payment.MarkAsSucceeded(Guid.NewGuid()));
    }

    [Fact]
    public void MarkAsFailed_FromProcessing_SetsFailureReason()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "CreditCard");
        payment.MarkAsProcessing();

        payment.MarkAsFailed("Insufficient funds");

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("Insufficient funds", payment.FailureReason);
    }

    [Fact]
    public void MarkAsFailed_WhenNotProcessing_Throws()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "CreditCard");

        Assert.Throws<InvalidPaymentOperationException>(() => payment.MarkAsFailed("some reason"));
    }
}
