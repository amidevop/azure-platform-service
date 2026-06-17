using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using AzurePlatformService.Api.Services;
using AzurePlatformService.Shared.Interfaces;
using AzurePlatformService.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzurePlatformService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkController : ControllerBase
{
    private readonly IWorkItemStore _workItemStore;
    private readonly IMessagePublisher _messagePublisher;
    private readonly CircuitBreakerService _circuitBreaker;

    public WorkController(
        IWorkItemStore workItemStore,
        IMessagePublisher messagePublisher,
        CircuitBreakerService circuitBreaker)
    {
        _workItemStore = workItemStore;
        _messagePublisher = messagePublisher;
        _circuitBreaker = circuitBreaker;
    }

    [HttpGet]
    public async Task<IActionResult> GetWorkItems()
    {
        try
        {
            var items = await _workItemStore.GetRecentAsync(100);

            var response = items.Select(item => new
            {
                id = item.Id,
                payload = item.Payload,
                processedAt = item.ProcessedAt,
                status = item.Status.ToString()
            });

            return Ok(response);
        }
        catch (Exception)
        {
            return StatusCode(503, new { message = "Service temporarily unavailable" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> PostWorkItem([FromBody] WorkItemRequest? request)
    {
        // Validate request body: reject null, empty, or whitespace-only payloads
        if (request == null || string.IsNullOrWhiteSpace(request.Payload))
        {
            return BadRequest(new { message = "Payload is required and cannot be empty or whitespace." });
        }

        var message = new WorkItemMessage
        {
            WorkItemId = Guid.NewGuid(),
            Payload = request.Payload,
            EnqueuedAt = DateTime.UtcNow,
            AttemptCount = 0
        };

        try
        {
            await _circuitBreaker.ExecuteAsync(async (ct) =>
            {
                await _messagePublisher.PublishAsync(message, ct);
            }, HttpContext.RequestAborted);

            return Accepted(new { id = message.WorkItemId, status = "Accepted" });
        }
        catch (CircuitBreakerOpenException)
        {
            return StatusCode(503, new { message = "Service temporarily unavailable. Circuit breaker is open." });
        }
        catch (ServiceBusException)
        {
            return StatusCode(503, new { message = "Service temporarily unavailable. Unable to reach message broker." });
        }
    }
}
