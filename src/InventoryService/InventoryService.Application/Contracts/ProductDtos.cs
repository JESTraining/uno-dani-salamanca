using System.ComponentModel.DataAnnotations;

namespace InventoryService.Application.Contracts;

public sealed record CreateProductRequest(
    [Required, MaxLength(50)] string Sku,
    [Required, MaxLength(200)] string Name,
    [MaxLength(1000)] string? Description,
    [Range(0.01, double.MaxValue)] decimal UnitPrice,
    [Range(0, int.MaxValue)] int StockQuantity);

public sealed record UpdateStockRequest([Range(0, int.MaxValue)] int StockQuantity);

public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    decimal UnitPrice,
    int StockQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
