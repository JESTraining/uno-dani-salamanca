using Asp.Versioning;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderService.API.Middleware;
using Prometheus;
using OrderService.Application.Abstractions;
using IntegrationEvents;
using OrderService.Application.Services;
using OrderService.Infrastructure.ExternalServices;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Persistence;
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
    .AddNpgSql(builder.Configuration.GetConnectionString("OrderDb")!, name: "postgres", tags: ["ready"])
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
    .ConfigureResource(resource => resource.AddService(serviceName: "OrderService"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddNpgsql()
        .AddSource("MassTransit")
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317")));

builder.Services.AddDbContext<OrderDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("OrderDb"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IIdempotencyStore, IdempotencyStore>();
builder.Services.AddScoped<IOrderManager, OrderManager>();

builder.Services.AddSignalR();

// IOrderEventPublisher fans out to both RabbitMQ and SignalR (Phase 4) via
// a composite. Registered as a factory rather than IEnumerable<IOrderEventPublisher>
// to avoid the composite recursively resolving itself.
builder.Services.AddScoped<RabbitMqOrderEventPublisher>();
builder.Services.AddScoped<SignalROrderEventPublisher>();
builder.Services.AddScoped<IOrderEventPublisher>(sp => new CompositeOrderEventPublisher(
    sp.GetRequiredService<RabbitMqOrderEventPublisher>(),
    sp.GetRequiredService<SignalROrderEventPublisher>()));

builder.Services.AddCors(options =>
{
    // Phase 4: the frontend calls this service directly (no API Gateway
    // yet, that is Phase 5) - see CLAUDE.md, "Architecture: Non-Negotiable
    // Rules" for the documented interim exception.
    options.AddPolicy("AllowFrontend", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddHttpClient<IInventoryAvailabilityChecker, InventoryHttpClient>(client =>
{
    var inventoryBaseUrl = builder.Configuration["Services:InventoryService:BaseUrl"]
        ?? throw new InvalidOperationException("Services:InventoryService:BaseUrl is not configured.");
    client.BaseAddress = new Uri(inventoryBaseUrl);
});

builder.Services.AddMassTransit(x =>
{
    // Choreographed saga (Phase 3): Order Service reacts to the outcomes
    // Payment/Inventory Service publish, same reactive style they already use.
    x.AddConsumer<PaymentProcessedConsumer>();
    x.AddConsumer<PaymentFailedConsumer>();
    x.AddConsumer<InventoryReservedConsumer>();
    x.AddConsumer<InventoryFailedConsumer>();

    // Exchange names default to the CLR type's full name (namespace +
    // type). Each service keeps its own local copy of a shared event in
    // its own namespace (see CLAUDE.md, "Established Architecture
    // Pattern"), so without this, the publisher and a consumer in another
    // service would bind to two different exchanges for "the same" event.
    // SetEntityName pins the exchange name to the contract name alone,
    // identical across every service that publishes or consumes it.
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Message<OrderCreatedEvent>(m => m.SetEntityName("OrderCreatedEvent"));
        cfg.Message<OrderStatusChangedEvent>(m => m.SetEntityName("OrderStatusChangedEvent"));
        cfg.Message<OrderCompletedEvent>(m => m.SetEntityName("OrderCompletedEvent"));
        cfg.Message<PaymentProcessedEvent>(m => m.SetEntityName("PaymentProcessedEvent"));
        cfg.Message<PaymentFailedEvent>(m => m.SetEntityName("PaymentFailedEvent"));
        cfg.Message<InventoryReservedEvent>(m => m.SetEntityName("InventoryReservedEvent"));
        cfg.Message<InventoryFailedEvent>(m => m.SetEntityName("InventoryFailedEvent"));

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

        cfg.ReceiveEndpoint("order-service-payment-processed", e => e.ConfigureConsumer<PaymentProcessedConsumer>(context));
        cfg.ReceiveEndpoint("order-service-payment-failed", e => e.ConfigureConsumer<PaymentFailedConsumer>(context));
        cfg.ReceiveEndpoint("order-service-inventory-reserved", e => e.ConfigureConsumer<InventoryReservedConsumer>(context));
        cfg.ReceiveEndpoint("order-service-inventory-failed", e => e.ConfigureConsumer<InventoryFailedConsumer>(context));
    });
});

var app = builder.Build();

// Applies pending migrations on startup (Phase 5: containers have no
// separate migration step, and EF Core migrations are idempotent - already-
// applied ones are skipped, so this is also safe to run against a fresh
// local dev database or a WebApplicationFactory-managed test one).
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseHttpMetrics();
app.MapControllers();
app.MapHub<OrderHub>("/hubs/orders");
app.MapMetrics();

app.MapHealthChecks("/health");
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapHealthChecks("/live", new HealthCheckOptions { Predicate = _ => false });

app.Run();

public partial class Program
{
}
