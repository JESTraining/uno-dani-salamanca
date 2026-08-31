using System.ComponentModel.DataAnnotations;

namespace PaymentService.Application.Contracts;

public sealed record ProcessPaymentRequest(
    [Required] Guid OrderId,
    [Range(0.01, double.MaxValue)] decimal Amount,
    string? PaymentMethod);

public sealed record PaymentResponse(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    string PaymentMethod,
    string Status,
    Guid? TransactionId,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);
