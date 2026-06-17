using System.Diagnostics;
using System.Text.Json;
using Azure.Identity;

namespace AzurePlatformService.Api.Middleware;

/// <summary>
/// Middleware that catches Azure Identity AuthenticationFailedException
/// and returns HTTP 503 within 5 seconds, logging to Application Insights.
/// </summary>
public class AuthenticationFailureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationFailureMiddleware> _logger;
    private static readonly TimeSpan AuthFailureTimeout = TimeSpan.FromSeconds(5);

    public AuthenticationFailureMiddleware(RequestDelegate next, ILogger<AuthenticationFailureMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            cts.CancelAfter(AuthFailureTimeout);

            await _next(context);
        }
        catch (AuthenticationFailedException ex)
        {
            await HandleAuthenticationFailureAsync(context, ex);
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            // Timeout occurred (not a client cancellation) — treat as auth failure timeout
            _logger.LogError("Authentication operation timed out after {Timeout}s", AuthFailureTimeout.TotalSeconds);

            Activity.Current?.SetStatus(ActivityStatusCode.Error, "Authentication timeout");
            Activity.Current?.SetTag("exception.type", nameof(TimeoutException));

            await WriteServiceUnavailableResponse(context, "Authentication operation timed out.");
        }
    }

    private async Task HandleAuthenticationFailureAsync(HttpContext context, AuthenticationFailedException ex)
    {
        _logger.LogError(ex,
            "Managed Identity authentication failed. Service: {Service}, Message: {Message}",
            ex.Source ?? "Unknown",
            ex.Message);

        // Record on the current OpenTelemetry span
        Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
        Activity.Current?.SetTag("exception.type", ex.GetType().Name);
        Activity.Current?.SetTag("auth.failure", true);

        await WriteServiceUnavailableResponse(context, "Authentication failure. Service temporarily unavailable.");
    }

    private static async Task WriteServiceUnavailableResponse(HttpContext context, string message)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";

            var response = new { message };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
