using OrderService.Application.Abstractions;
using OrderService.Application.Contracts;
using OrderService.Application.Exceptions;
using OrderService.Domain;

namespace OrderService.Application.Services;

public class OrderManager : IOrderManager
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryAvailabilityChecker _inventoryChecker;
    private readonly IOrderEventPublisher _eventPublisher;
    private readonly IIdempotencyStore _idempotencyStore;

    public OrderManager(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IInventoryAvailabilityChecker inventoryChecker,
        IOrderEventPublisher eventPublisher,
        IIdempotencyStore idempotencyStore)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _inventoryChecker = inventoryChecker;
        _eventPublisher = eventPublisher;
        _idempotencyStore = idempotencyStore;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingOrderId = await _idempotencyStore.FindExistingOrderIdAsync(idempotencyKey, cancellationToken);
            if (existingOrderId is not null)
            {
                var existingOrder = await _orderRepository.GetByIdAsync(existingOrderId.Value, cancellationToken);
                if (existingOrder is not null)
                    return MapToResponse(existingOrder);
            }
        }

        var stockCheckItems = request.Items
            .Select(i => new StockCheckItem(i.ProductId, i.Quantity))
            .ToList();

        var stockAvailable = await _inventoryChecker.IsStockAvailableAsync(stockCheckItems, cancellationToken);
        if (!stockAvailable)
            throw new InsufficientStockException();

        var orderItems = request.Items
            .Select(i => new OrderItem(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice))
            .ToList();

        var order = Order.Create(request.CustomerId, request.CustomerName, request.CustomerEmail, orderItems);

        await _orderRepository.AddAsync(order, cancellationToken);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            await _idempotencyStore.SaveAsync(idempotencyKey, order.Id, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _eventPublisher.PublishOrderCreatedAsync(order, cancellationToken);

        return MapToResponse(order);
    }

    public async Task<OrderResponse?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken);
        return order is null ? null : MapToResponse(order);
    }

    public async Task<PagedResult<OrderResponse>> ListOrdersAsync(OrderListQuery query, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _orderRepository.ListAsync(query, cancellationToken);
        return new PagedResult<OrderResponse>(
            items.Select(MapToResponse).ToList(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<OrderResponse> UpdateStatusAsync(Guid id, OrderStatus newStatus, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new OrderNotFoundException(id);

        var previousStatus = order.Status;
        order.ChangeStatus(newStatus);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _eventPublisher.PublishOrderStatusChangedAsync(order.Id, previousStatus, order.Status, cancellationToken);

        return MapToResponse(order);
    }

    public async Task<OrderResponse> CancelOrderAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new OrderNotFoundException(id);

        var previousStatus = order.Status;
        order.Cancel();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _eventPublisher.PublishOrderStatusChangedAsync(order.Id, previousStatus, order.Status, cancellationToken);

        return MapToResponse(order);
    }

    private static OrderResponse MapToResponse(Order order) => new(
        order.Id,
        order.CustomerId,
        order.CustomerName,
        order.CustomerEmail,
        order.Status.ToString(),
        order.TotalAmount,
        order.CreatedAt,
        order.UpdatedAt,
        order.Items.Select(i => new OrderItemResponse(i.Id, i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.Subtotal)).ToList());
}
