using System.Text;
using Asp.Versioning;
using InventoryService.API.Middleware;
using InventoryService.Application.Abstractions;
using IntegrationEvents;
using InventoryService.Application.Services;
using InventoryService.Infrastructure.BackgroundServices;
using InventoryService.Infrastructure.Messaging;
using InventoryService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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
    .AddNpgSql(builder.Configuration.GetConnectionString("InventoryDb")!, name: "postgres", tags: ["ready"])
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

// Validates tokens issued by the Gateway (shared signing key/issuer/audience
// via the "Jwt" config section) - this service never issues tokens itself,
// only enforces [Authorize] on the one endpoint CLAUDE.md's Security section
// calls out (product creation, admin only). See ApiGateway.API for issuance.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

// Tracing exported to Jaeger via OTLP. Metrics use prometheus-net instead of
// OpenTelemetry's own Prometheus exporter, which has never had a stable
// release (still beta-only years in) - prometheus-net is the mature,
// stable, widely-used .NET Prometheus client, wired via UseHttpMetrics()/
// MapMetrics() below rather than through the OpenTelemetry metrics pipeline.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: "InventoryService"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddNpgsql()
        .AddSource("MassTransit")
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317")));

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

        // Task 3.1: 3 retries with exponential backoff before MassTransit's
        // RabbitMQ transport moves the message to the receive endpoint's
        // automatically-created "<queue>_error" dead-letter queue.
        cfg.UseMessageRetry(r => r.Exponential(
            3,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(5)));

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
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
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
