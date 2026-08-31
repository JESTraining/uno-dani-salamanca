using Moq;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Services;
using PaymentService.Domain;

namespace PaymentService.Tests.Application;

public class PaymentManagerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPaymentGatewayClient> _gateway = new();
    private readonly Mock<IPaymentEventPublisher> _eventPublisher = new();
    private readonly PaymentManager _sut;

    public PaymentManagerTests()
    {
        _sut = new PaymentManager(_paymentRepository.Object, _unitOfWork.Object, _gateway.Object, _eventPublisher.Object);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WithExistingPaymentForOrder_ReturnsExistingWithoutCallingGateway()
    {
        var orderId = Guid.NewGuid();
        var existing = Payment.Create(orderId, 100m, "CreditCard");
        _paymentRepository.Setup(r => r.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var response = await _sut.ProcessPaymentAsync(orderId, 100m, null, default);

        Assert.Equal(existing.Id, response.Id);
        _gateway.Verify(g => g.ChargeAsync(It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
        _paymentRepository.Verify(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenGatewaySucceeds_PublishesPaymentProcessedEvent()
    {
        var orderId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        _paymentRepository.Setup(r => r.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync((Payment?)null);
        _gateway.Setup(g => g.ChargeAsync(100m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayChargeResult(true, transactionId, null));

        var response = await _sut.ProcessPaymentAsync(orderId, 100m, null, default);

        Assert.Equal("Succeeded", response.Status);
        Assert.Equal(transactionId, response.TransactionId);
        _eventPublisher.Verify(p => p.PublishPaymentProcessedAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisher.Verify(p => p.PublishPaymentFailedAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenGatewayFails_PublishesPaymentFailedEvent()
    {
        var orderId = Guid.NewGuid();
        _paymentRepository.Setup(r => r.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync((Payment?)null);
        _gateway.Setup(g => g.ChargeAsync(100m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayChargeResult(false, null, "Payment declined by issuing bank."));

        var response = await _sut.ProcessPaymentAsync(orderId, 100m, null, default);

        Assert.Equal("Failed", response.Status);
        _eventPublisher.Verify(p => p.PublishPaymentFailedAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisher.Verify(p => p.PublishPaymentProcessedAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByOrderIdAsync_WhenNoPaymentExists_ReturnsNull()
    {
        _paymentRepository.Setup(r => r.GetByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Payment?)null);

        var response = await _sut.GetByOrderIdAsync(Guid.NewGuid(), default);

        Assert.Null(response);
    }
}
