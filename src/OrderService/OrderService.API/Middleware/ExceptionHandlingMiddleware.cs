using Microsoft.EntityFrameworkCore;
using OrderService.Application.Exceptions;
using OrderService.Domain;

namespace OrderService.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OrderNotFoundException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, ex.Message);
        }
        catch (InvalidOrderOperationException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, ex.Message);
        }
        catch (InsufficientStockException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status422UnprocessableEntity, ex.Message);
        }
        catch (InventoryServiceUnavailableException ex)
        {
            _logger.LogWarning(ex, "Inventory Service unavailable while creating an order.");
            await WriteProblemAsync(context, StatusCodes.Status503ServiceUnavailable, ex.Message);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict while updating an order.");
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "The order was modified by another request. Please retry.");
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(new { status = statusCode, title = detail });
    }
}
