using Azure.Messaging.ServiceBus;
using AzurePlatformService.Shared.Interfaces;

namespace AzurePlatformService.Api.HealthChecks;

/// <summary>
/// Checks Azure Service Bus connectivity by peeking at the queue.
/// </summary>
public class ServiceBusHealthCheck : IDependencyHealthCheck
{
    private readonly ServiceBusClient _client;
    private readonly string _queueName;

    public string DependencyName => "ServiceBus";

    public ServiceBusHealthCheck(ServiceBusClient client, IConfiguration configuration)
    {
        _client = client;
        _queueName = configuration.GetValue<string>("ServiceBus:QueueName") ?? "work-items";
    }

    public async Task<(bool IsHealthy, string? Reason)> CheckHealthAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            var receiver = _client.CreateReceiver(_queueName);
            await using (receiver.ConfigureAwait(false))
            {
                // Peek a message to verify connectivity (does not consume messages)
                await receiver.PeekMessageAsync(cancellationToken: cts.Token).ConfigureAwait(false);
            }
            return (true, null);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return (false, "Connection timeout");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
