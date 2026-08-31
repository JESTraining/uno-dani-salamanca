using System.ComponentModel.DataAnnotations;

namespace OrderService.Application.Contracts;

public sealed record CreateOrderItemRequest(
    [Required] Guid ProductId,
    [Required, MaxLength(200)] string ProductName,
    [Range(1, int.MaxValue)] int Quantity,
    [Range(0.01, double.MaxValue)] decimal UnitPrice);

public sealed record CreateOrderRequest(
    [Required] Guid CustomerId,
    [Required, MaxLength(200)] string CustomerName,
    [Required, EmailAddress, MaxLength(200)] string CustomerEmail,
    [Required, MinLength(1)] IReadOnlyCollection<CreateOrderItemRequest> Items);

public sealed record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyCollection<OrderItemResponse> Items);

public sealed record UpdateOrderStatusRequest([Required] string Status);

public sealed record OrderListQuery(
    int Page,
    int PageSize,
    string? Status,
    DateTime? DateFrom,
    DateTime? DateTo,
    Guid? CustomerId);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
