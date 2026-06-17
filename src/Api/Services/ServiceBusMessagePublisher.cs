using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using AzurePlatformService.Shared.Interfaces;
using AzurePlatformService.Shared.Models;

namespace AzurePlatformService.Api.Services;

/// <summary>
/// Publishes work item messages to Azure Service Bus with OpenTelemetry trace context propagation.
/// </summary>
public class ServiceBusMessagePublisher : IMessagePublisher
{
    private readonly ServiceBusSender _sender;

    public ServiceBusMessagePublisher(ServiceBusSender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public async Task PublishAsync(WorkItemMessage message, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(message);
        var serviceBusMessage = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            MessageId = message.WorkItemId.ToString()
        };

        // Propagate OpenTelemetry trace context (W3C traceparent) in message application properties
        var activity = Activity.Current;
        if (activity != null)
        {
            serviceBusMessage.ApplicationProperties["traceparent"] = activity.Id ?? string.Empty;
        }

        await _sender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }
}
