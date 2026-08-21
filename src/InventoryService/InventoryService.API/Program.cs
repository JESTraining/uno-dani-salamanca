using InventoryService.API.Middleware;
using InventoryService.Application.Abstractions;
using IntegrationEvents;
using InventoryService.Application.Services;
using InventoryService.Infrastructure.BackgroundServices;
using InventoryService.Infrastructure.Messaging;
using InventoryService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("InventoryDb"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IOrderItemSnapshotStore, OrderItemSnapshotStore>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IProductManager, ProductManager>();
builder.Services.AddScoped<IReservationManager, ReservationManager>();
builder.Services.AddScoped<IInventoryEventPublisher, RabbitMqInventoryEventPublisher>();

builder.Services.AddHostedService<ExpiredReservationCleanupService>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();
    x.AddConsumer<PaymentProcessedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        // Exchange names must be pinned explicitly and identically across
        // every service that touches a given event - see the matching
        // comment in OrderService.API/Program.cs for why.
        cfg.Message<OrderCreatedEvent>(m => m.SetEntityName("OrderCreatedEvent"));
        cfg.Message<PaymentProcessedEvent>(m => m.SetEntityName("PaymentProcessedEvent"));
        cfg.Message<InventoryReservedEvent>(m => m.SetEntityName("InventoryReservedEvent"));
        cfg.Message<InventoryFailedEvent>(m => m.SetEntityName("InventoryFailedEvent"));

        cfg.Host(builder.Configuration["RabbitMq:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("inventory-service-order-created", e =>
        {
            e.ConfigureConsumer<OrderCreatedConsumer>(context);
        });

        cfg.ReceiveEndpoint("inventory-service-payment-processed", e =>
        {
            e.ConfigureConsumer<PaymentProcessedConsumer>(context);
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
