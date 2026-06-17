using System.Diagnostics;
using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using AzurePlatformService.Shared.Interfaces;
using AzurePlatformService.Shared.Models;

namespace AzurePlatformService.Worker;

public class WorkerService : BackgroundService
{
    private readonly ILogger<WorkerService> _logger;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly IWorkItemStore _workItemStore;
    private readonly IRetryPolicy _retryPolicy;

    private static readonly ActivitySource ActivitySource = new("AzurePlatformService.Worker");

    private ServiceBusProcessor? _processor;

    public WorkerService(
        ILogger<WorkerService> logger,
        ServiceBusClient serviceBusClient,
        IWorkItemStore workItemStore,
        IRetryPolicy retryPolicy)
    {
        _logger = logger;
        _serviceBusClient = serviceBusClient;
        _workItemStore = workItemStore;
        _retryPolicy = retryPolicy;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker Service starting at: {Time}", DateTimeOffset.Now);

        try
        {
            _processor = _serviceBusClient.CreateProcessor("work-items", new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1,
                PrefetchCount = 0
            });

            _processor.ProcessMessageAsync += ProcessMessageAsync;
            _processor.ProcessErrorAsync += ProcessErrorAsync;

            await _processor.StartProcessingAsync(stoppingToken);
        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogError(ex,
                "Managed Identity authentication failed during Worker startup. Service: {Source}, Message: {Message}",
                ex.Source ?? "Unknown", ex.Message);

            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
            Activity.Current?.SetTag("exception.type", ex.GetType().Name);
            Activity.Current?.SetTag("auth.failure", true);

            // Rethrow to stop the host — the Worker cannot function without auth
            throw;
        }

        _logger.LogInformation("Worker Service started, listening for messages");

        // Keep the service running until cancellation is requested
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker Service stopping");

        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    internal async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        using var activity = StartActivityFromMessage(args.Message);

        try
        {
            var message = DeserializeMessage(args.Message);

            await ProcessWithRetryAsync(message, args, activity);
        }
        catch (Exception ex) when (IsNonTransientError(ex))
        {
            _logger.LogError(ex, "Non-transient error processing message {MessageId}. Dead-lettering immediately.",
                args.Message.MessageId);

            RecordErrorOnActivity(activity, ex);

            await args.DeadLetterMessageAsync(args.Message, new Dictionary<string, object>
            {
                ["DeadLetterReason"] = "NonTransientError",
                ["DeadLetterErrorDescription"] = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing message {MessageId}",
                args.Message.MessageId);

            RecordErrorOnActivity(activity, ex);

            await args.DeadLetterMessageAsync(args.Message, new Dictionary<string, object>
            {
                ["DeadLetterReason"] = "UnexpectedError",
                ["DeadLetterErrorDescription"] = ex.Message
            });
        }
    }

    private async Task ProcessWithRetryAsync(WorkItemMessage message, ProcessMessageEventArgs args, Activity? activity)
    {
        var attempt = 0;
        var maxRetries = _retryPolicy.MaxRetries;

        while (true)
        {
            attempt++;
            try
            {
                var workItem = new WorkItem
                {
                    Id = message.WorkItemId,
                    Payload = message.Payload,
                    ProcessedAt = DateTime.UtcNow,
                    Status = WorkItemStatus.Completed
                };

                await _workItemStore.AddAsync(workItem);

                _logger.LogInformation("Successfully processed work item {WorkItemId} on attempt {Attempt}",
                    message.WorkItemId, attempt);

                await args.CompleteMessageAsync(args.Message);
                return;
            }
            catch (Exception ex) when (IsNonTransientError(ex))
            {
                // Non-transient errors should not be retried - rethrow to be caught by outer handler
                throw;
            }
            catch (Exception ex) when (IsTransientError(ex))
            {
                if (attempt >= maxRetries)
                {
                    _logger.LogWarning(ex,
                        "Max retries ({MaxRetries}) exhausted for work item {WorkItemId}. Dead-lettering.",
                        maxRetries, message.WorkItemId);

                    RecordErrorOnActivity(activity, ex);

                    await args.DeadLetterMessageAsync(args.Message, new Dictionary<string, object>
                    {
                        ["DeadLetterReason"] = "MaxRetriesExhausted",
                        ["DeadLetterErrorDescription"] = $"Failed after {maxRetries} attempts: {ex.Message}"
                    });
                    return;
                }

                var delay = _retryPolicy.ComputeDelay(attempt);
                _logger.LogWarning(ex,
                    "Transient error on attempt {Attempt}/{MaxRetries} for work item {WorkItemId}. Retrying in {Delay}ms.",
                    attempt, maxRetries, message.WorkItemId, delay.TotalMilliseconds);

                await Task.Delay(delay);
            }
        }
    }

    private Activity? StartActivityFromMessage(ServiceBusReceivedMessage message)
    {
        var traceparent = ExtractTraceparent(message);

        if (traceparent is not null && IsValidTraceparent(traceparent))
        {
            var activityContext = ParseTraceparent(traceparent);
            var activity = ActivitySource.StartActivity(
                "ProcessWorkItem",
                ActivityKind.Consumer,
                activityContext);

            return activity;
        }
        else
        {
            // Create a new root span when traceparent is missing or malformed
            var activity = ActivitySource.StartActivity(
                "ProcessWorkItem",
                ActivityKind.Consumer);

            if (activity is not null)
            {
                activity.SetTag("trace.context.missing", true);
                _logger.LogWarning("Trace context missing or malformed for message {MessageId}. Created new root span.",
                    message.MessageId);
            }

            return activity;
        }
    }

    private static string? ExtractTraceparent(ServiceBusReceivedMessage message)
    {
        if (message.ApplicationProperties.TryGetValue("traceparent", out var value) && value is string traceparent)
        {
            return traceparent;
        }

        return null;
    }

    internal static bool IsValidTraceparent(string traceparent)
    {
        // W3C Trace Context format: "00-{traceId}-{spanId}-{flags}"
        // traceId: 32 hex chars, spanId: 16 hex chars, flags: 2 hex chars
        var parts = traceparent.Split('-');
        if (parts.Length != 4)
            return false;

        if (parts[0] != "00")
            return false;

        if (parts[1].Length != 32 || !IsHexString(parts[1]))
            return false;

        if (parts[2].Length != 16 || !IsHexString(parts[2]))
            return false;

        if (parts[3].Length != 2 || !IsHexString(parts[3]))
            return false;

        // traceId and spanId must not be all zeros
        if (parts[1] == new string('0', 32))
            return false;

        if (parts[2] == new string('0', 16))
            return false;

        return true;
    }

    private static bool IsHexString(string value)
    {
        foreach (var c in value)
        {
            if (!char.IsAsciiHexDigit(c))
                return false;
        }
        return true;
    }

    private static ActivityContext ParseTraceparent(string traceparent)
    {
        var parts = traceparent.Split('-');
        var traceId = ActivityTraceId.CreateFromString(parts[1].AsSpan());
        var spanId = ActivitySpanId.CreateFromString(parts[2].AsSpan());
        var flags = (ActivityTraceFlags)Convert.ToInt32(parts[3], 16);

        return new ActivityContext(traceId, spanId, flags, isRemote: true);
    }

    private static WorkItemMessage DeserializeMessage(ServiceBusReceivedMessage message)
    {
        var body = message.Body.ToString();
        var workItemMessage = JsonSerializer.Deserialize<WorkItemMessage>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (workItemMessage is null)
        {
            throw new JsonException("Failed to deserialize message body to WorkItemMessage");
        }

        return workItemMessage;
    }

    internal static bool IsTransientError(Exception ex)
    {
        return ex is TimeoutException
            || ex is HttpRequestException
            || ex is OperationCanceledException
            || (ex is ServiceBusException sbEx && sbEx.IsTransient);
    }

    internal static bool IsNonTransientError(Exception ex)
    {
        return ex is JsonException
            || ex is FormatException
            || ex is InvalidOperationException
            || ex is ArgumentException;
    }

    private static void RecordErrorOnActivity(Activity? activity, Exception ex)
    {
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.SetTag("exception.type", ex.GetType().Name);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        if (args.Exception is AuthenticationFailedException authEx)
        {
            _logger.LogError(authEx,
                "Managed Identity authentication failed during message processing. Source: {ErrorSource}, Message: {Message}",
                args.ErrorSource, authEx.Message);

            Activity.Current?.SetStatus(ActivityStatusCode.Error, authEx.Message);
            Activity.Current?.SetTag("exception.type", authEx.GetType().Name);
            Activity.Current?.SetTag("auth.failure", true);
        }
        else
        {
            _logger.LogError(args.Exception,
                "Error in Service Bus processor. Source: {ErrorSource}, Namespace: {Namespace}, Entity: {Entity}",
                args.ErrorSource, args.FullyQualifiedNamespace, args.EntityPath);
        }

        return Task.CompletedTask;
    }
}
