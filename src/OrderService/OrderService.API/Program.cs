using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.API.Middleware;
using OrderService.Application.Abstractions;
using OrderService.Application.Services;
using OrderService.Infrastructure.ExternalServices;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<OrderDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("OrderDb"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IIdempotencyStore, IdempotencyStore>();
builder.Services.AddScoped<IOrderManager, OrderManager>();
builder.Services.AddScoped<IOrderEventPublisher, RabbitMqOrderEventPublisher>();

builder.Services.AddHttpClient<IInventoryAvailabilityChecker, InventoryHttpClient>(client =>
{
    var inventoryBaseUrl = builder.Configuration["Services:InventoryService:BaseUrl"]
        ?? throw new InvalidOperationException("Services:InventoryService:BaseUrl is not configured.");
    client.BaseAddress = new Uri(inventoryBaseUrl);
});

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program
{
}
