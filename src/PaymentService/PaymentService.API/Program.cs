using MassTransit;
using Microsoft.EntityFrameworkCore;
using PaymentService.API.Middleware;
using PaymentService.Application.Abstractions;
using IntegrationEvents;
using PaymentService.Application.Services;
using PaymentService.Infrastructure.ExternalServices;
using PaymentService.Infrastructure.Messaging;
using PaymentService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("PaymentDb"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IPaymentManager, PaymentManager>();
builder.Services.AddScoped<IPaymentEventPublisher, RabbitMqPaymentEventPublisher>();
builder.Services.AddScoped<IPaymentGatewayClient, MockPaymentGateway>();
builder.Services.AddSingleton<IRandomProvider, SystemRandomProvider>();
builder.Services.AddSingleton<IPaymentProcessingDelay, RandomPaymentProcessingDelay>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        // Exchange names must be pinned explicitly and identically across
        // every service that touches a given event - see the matching
        // comment in OrderService.API/Program.cs for why.
        cfg.Message<OrderCreatedEvent>(m => m.SetEntityName("OrderCreatedEvent"));
        cfg.Message<PaymentProcessedEvent>(m => m.SetEntityName("PaymentProcessedEvent"));
        cfg.Message<PaymentFailedEvent>(m => m.SetEntityName("PaymentFailedEvent"));

        cfg.Host(builder.Configuration["RabbitMq:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("payment-service-order-created", e =>
        {
            e.ConfigureConsumer<OrderCreatedConsumer>(context);
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
