using Asp.Versioning;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PaymentService.API.Middleware;
using PaymentService.Application.Abstractions;
using IntegrationEvents;
using PaymentService.Application.Services;
using PaymentService.Infrastructure.ExternalServices;
using PaymentService.Infrastructure.Messaging;
using PaymentService.Infrastructure.Persistence;
using Prometheus;
using RabbitMQ.Client;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddMvc().AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("PaymentDb")!, name: "postgres", tags: ["ready"])
    .AddRabbitMQ(
        factory: _ =>
        {
            var connectionFactory = new ConnectionFactory
            {
                Uri = new Uri($"amqp://{builder.Configuration["RabbitMq:Username"]}:{builder.Configuration["RabbitMq:Password"]}@{builder.Configuration["RabbitMq:Host"]}:5672"),
            };
            return connectionFactory.CreateConnectionAsync();
        },
        name: "rabbitmq",
        tags: ["ready"]);

// Tracing exported to Jaeger via OTLP. Metrics use prometheus-net instead of
// OpenTelemetry's own Prometheus exporter, which has never had a stable
// release (still beta-only years in) - prometheus-net is the mature,
// stable, widely-used .NET Prometheus client, wired via UseHttpMetrics()/
// MapMetrics() below rather than through the OpenTelemetry metrics pipeline.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: "PaymentService"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddNpgsql()
        .AddSource("MassTransit")
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317")));

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

        // Task 3.1: 3 retries with exponential backoff before MassTransit's
        // RabbitMQ transport moves the message to the receive endpoint's
        // automatically-created "<queue>_error" dead-letter queue.
        cfg.UseMessageRetry(r => r.Exponential(
            3,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(5)));

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
app.UseHttpMetrics();
app.MapControllers();
app.MapMetrics();

app.MapHealthChecks("/health");
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapHealthChecks("/live", new HealthCheckOptions { Predicate = _ => false });

app.Run();

public partial class Program
{
}
