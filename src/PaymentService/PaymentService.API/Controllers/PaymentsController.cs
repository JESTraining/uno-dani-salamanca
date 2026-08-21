using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Contracts;

namespace PaymentService.API.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentManager _paymentManager;

    public PaymentsController(IPaymentManager paymentManager)
    {
        _paymentManager = paymentManager;
    }

    [HttpPost("process")]
    public async Task<ActionResult<PaymentResponse>> Process([FromBody] ProcessPaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _paymentManager.ProcessPaymentAsync(request.OrderId, request.Amount, request.PaymentMethod, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<PaymentResponse>> GetByOrderId(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await _paymentManager.GetByOrderIdAsync(orderId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
