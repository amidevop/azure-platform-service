using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;
using AzurePlatformService.Shared.Interfaces;
using AzurePlatformService.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Register DefaultAzureCredential as a shared singleton for Managed Identity authentication
var credential = new DefaultAzureCredential();

// Register ServiceBusClient
var serviceBusConnectionString = builder.Configuration.GetValue<string>("ServiceBus:ConnectionString");
if (!string.IsNullOrEmpty(serviceBusConnectionString))
{
    // Development: connection string fallback
    builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnectionString));
}
else
{
    // Production: use Managed Identity via DefaultAzureCredential
    var fullyQualifiedNamespace = builder.Configuration.GetValue<string>("ServiceBus:FullyQualifiedNamespace")
        ?? builder.Configuration.GetValue<string>("ServiceBus:Namespace")
        ?? throw new InvalidOperationException(
            "Either ServiceBus:ConnectionString, ServiceBus:FullyQualifiedNamespace, or ServiceBus:Namespace must be configured.");

    builder.Services.AddSingleton(new ServiceBusClient(fullyQualifiedNamespace, credential));
}

// Register Key Vault SecretClient using Managed Identity (DefaultAzureCredential)
var keyVaultUri = builder.Configuration.GetValue<string>("KeyVault:VaultUri");
if (!string.IsNullOrEmpty(keyVaultUri))
{
    builder.Services.AddSingleton(new SecretClient(new Uri(keyVaultUri), credential));
}

// Register data store
builder.Services.AddSingleton<IWorkItemStore, InMemoryWorkItemStore>();

// Register retry policy (exponential backoff: 2s, 4s, 8s)
builder.Services.AddSingleton<IRetryPolicy>(new ExponentialBackoffRetryPolicy(
    baseDelay: TimeSpan.FromSeconds(2),
    multiplier: 2.0,
    maxRetries: 3));

// Register Worker hosted service
builder.Services.AddHostedService<WorkerService>();

var host = builder.Build();
host.Run();
