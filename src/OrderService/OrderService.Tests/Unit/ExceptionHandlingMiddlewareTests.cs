using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderService.API.Middleware;
using OrderService.Application.Exceptions;
using OrderService.Domain;

namespace OrderService.Tests.Unit;

public class ExceptionHandlingMiddlewareTests
{
    private static async Task<HttpContext> InvokeWithThrowingNextAsync(Exception exception)
    {
        var middleware = new ExceptionHandlingMiddleware(_ => throw exception, NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);
        return context;
    }

    [Fact]
    public async Task InvokeAsync_OrderNotFoundException_MapsTo404()
    {
        var context = await InvokeWithThrowingNextAsync(new OrderNotFoundException(Guid.NewGuid()));
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_InvalidOrderOperationException_MapsTo409()
    {
        var context = await InvokeWithThrowingNextAsync(new InvalidOrderOperationException("not allowed"));
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_InsufficientStockException_MapsTo422()
    {
        var context = await InvokeWithThrowingNextAsync(new InsufficientStockException());
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_InventoryServiceUnavailableException_MapsTo503()
    {
        var context = await InvokeWithThrowingNextAsync(
            new InventoryServiceUnavailableException("unreachable", new HttpRequestException()));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_DbUpdateConcurrencyException_MapsTo409()
    {
        var context = await InvokeWithThrowingNextAsync(new DbUpdateConcurrencyException());
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }
}
