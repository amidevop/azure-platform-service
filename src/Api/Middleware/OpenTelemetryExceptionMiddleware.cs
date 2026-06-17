using System.Diagnostics;

namespace AzurePlatformService.Api.Middleware;

/// <summary>
/// Middleware that captures unhandled exceptions and records them on the current
/// OpenTelemetry activity (span). Sets the span status to Error and adds the
/// exception type as a span attribute.
/// </summary>
public class OpenTelemetryExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OpenTelemetryExceptionMiddleware> _logger;

    public OpenTelemetryExceptionMiddleware(RequestDelegate next, ILogger<OpenTelemetryExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                // Record span status as Error
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);

                // Add exception type as a span attribute
                activity.SetTag("exception.type", ex.GetType().FullName);

                // Record the exception event on the span
                activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
                {
                    { "exception.type", ex.GetType().FullName },
                    { "exception.message", ex.Message },
                    { "exception.stacktrace", ex.StackTrace ?? string.Empty }
                }));
            }

            _logger.LogError(ex, "Unhandled exception occurred while processing request {Method} {Path}",
                context.Request.Method, context.Request.Path);

            // Re-throw to let ASP.NET Core handle the response
            throw;
        }
    }
}
