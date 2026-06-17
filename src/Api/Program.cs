using System.Diagnostics;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Security.KeyVault.Secrets;
using AzurePlatformService.Api;
using AzurePlatformService.Api.HealthChecks;
using AzurePlatformService.Api.Middleware;
using AzurePlatformService.Api.Services;
using AzurePlatformService.Shared.Interfaces;
using AzurePlatformService.Shared.Models;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IWorkItemStore, InMemoryWorkItemStore>();

// Configure OpenTelemetry
var serviceName = "AzurePlatformService.Api";
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: serviceName,
        serviceVersion: serviceVersion))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(serviceName)
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
            });

        if (!string.IsNullOrEmpty(appInsightsConnectionString))
        {
            tracing.AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = appInsightsConnectionString;
            });
        }
    });

// Configure logging to include trace/correlation IDs
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;

    if (!string.IsNullOrEmpty(appInsightsConnectionString))
    {
        logging.AddAzureMonitorLogExporter(options =>
        {
            options.ConnectionString = appInsightsConnectionString;
        });
    }
});

// Register the custom ActivitySource for manual instrumentation
builder.Services.AddSingleton(new ActivitySource(serviceName));

// Register circuit breaker as singleton (shared state across requests)
builder.Services.AddSingleton<CircuitBreakerService>();

// Register DefaultAzureCredential as a shared singleton for Managed Identity authentication
builder.Services.AddSingleton<DefaultAzureCredential>(_ => new DefaultAzureCredential());

// Register Service Bus client and message publisher
var serviceBusNamespace = builder.Configuration["ServiceBus:Namespace"];
var serviceBusQueueName = builder.Configuration["ServiceBus:QueueName"] ?? "work-items";

if (!string.IsNullOrEmpty(serviceBusNamespace))
{
    // Production: use Managed Identity via DefaultAzureCredential
    builder.Services.AddSingleton<ServiceBusClient>(sp =>
    {
        var credential = sp.GetRequiredService<DefaultAzureCredential>();
        return new ServiceBusClient(serviceBusNamespace, credential);
    });
    builder.Services.AddSingleton<ServiceBusSender>(sp =>
    {
        var client = sp.GetRequiredService<ServiceBusClient>();
        return client.CreateSender(serviceBusQueueName);
    });
    builder.Services.AddSingleton<IMessagePublisher, ServiceBusMessagePublisher>();
}
else
{
    var connectionString = builder.Configuration["ServiceBus:ConnectionString"];
    if (!string.IsNullOrEmpty(connectionString))
    {
        // Development: connection string fallback
        builder.Services.AddSingleton<ServiceBusClient>(sp =>
        {
            return new ServiceBusClient(connectionString);
        });
        builder.Services.AddSingleton<ServiceBusSender>(sp =>
        {
            var client = sp.GetRequiredService<ServiceBusClient>();
            return client.CreateSender(serviceBusQueueName);
        });
        builder.Services.AddSingleton<IMessagePublisher, ServiceBusMessagePublisher>();
    }
    else
    {
        // No Service Bus configured: register a placeholder that throws on use.
        // This allows the application to start and serve other endpoints (health checks, GET).
        // Tests override IMessagePublisher with a mock.
        // Register a null-client for health check (it will report unhealthy)
        builder.Services.AddSingleton<ServiceBusClient>(sp =>
            new ServiceBusClient("Endpoint=sb://placeholder.servicebus.windows.net/;SharedAccessKeyName=placeholder;SharedAccessKey=placeholder"));
        builder.Services.AddSingleton<IMessagePublisher>(sp =>
            new UnconfiguredMessagePublisher());
    }
}

// Register Key Vault SecretClient using Managed Identity (DefaultAzureCredential)
var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    builder.Services.AddSingleton<SecretClient>(sp =>
    {
        var credential = sp.GetRequiredService<DefaultAzureCredential>();
        return new SecretClient(new Uri(keyVaultUri), credential);
    });
}

// Register health checks with liveness tag
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
        tags: new[] { "live" });

// Register dependency health checks for readiness probe
builder.Services.AddSingleton<IDependencyHealthCheck, ServiceBusHealthCheck>();
builder.Services.AddSingleton<IDependencyHealthCheck, ApplicationInsightsHealthCheck>();

var app = builder.Build();

// Add OpenTelemetry exception-handling middleware (records span status and exception type)
app.UseMiddleware<OpenTelemetryExceptionMiddleware>();

// Add middleware to handle authentication failures (returns 503 within 5s)
app.UseMiddleware<AuthenticationFailureMiddleware>();

app.MapControllers();

app.Run();

namespace AzurePlatformService.Api
{
    public partial class Program { }

    /// <summary>
    /// Placeholder IMessagePublisher used when Service Bus is not configured.
    /// Throws InvalidOperationException on publish attempts.
    /// </summary>
    internal sealed class UnconfiguredMessagePublisher : IMessagePublisher
    {
        public Task PublishAsync(WorkItemMessage message, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Service Bus is not configured. Set ServiceBus:Namespace or ServiceBus:ConnectionString in configuration.");
        }
    }
}
