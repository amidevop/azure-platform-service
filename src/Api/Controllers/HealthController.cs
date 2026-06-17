using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using AzurePlatformService.Shared.Interfaces;
using AzurePlatformService.Shared.Models;

namespace AzurePlatformService.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly IEnumerable<IDependencyHealthCheck> _dependencyChecks;
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<HealthController> _logger;

    private static readonly TimeSpan DependencyTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(5);

    public HealthController(
        IEnumerable<IDependencyHealthCheck> dependencyChecks,
        HealthCheckService healthCheckService,
        ILogger<HealthController> logger)
    {
        _dependencyChecks = dependencyChecks;
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    /// <summary>
    /// Liveness probe - indicates whether the application is running.
    /// Returns 200 when healthy, 503 on internal errors.
    /// </summary>
    [HttpGet("live")]
    public async Task<IActionResult> Live(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(OverallTimeout);

            var report = await _healthCheckService.CheckHealthAsync(
                registration => registration.Tags.Contains("live"),
                cts.Token).ConfigureAwait(false);

            if (report.Status == HealthStatus.Healthy)
            {
                return Ok(new HealthCheckResponse { Status = "Healthy" });
            }

            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new HealthCheckResponse { Status = "Unhealthy" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Liveness check failed due to internal error");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new HealthCheckResponse { Status = "Unhealthy" });
        }
    }

    /// <summary>
    /// Readiness probe - checks downstream dependency connectivity.
    /// Returns 200 when all dependencies are healthy, 503 otherwise.
    /// </summary>
    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        using var overallCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overallCts.CancelAfter(OverallTimeout);

        var dependencies = new List<DependencyHealth>();
        var allHealthy = true;

        try
        {
            // Check all dependencies in parallel with individual 3-second timeouts
            var checkTasks = _dependencyChecks.Select(async check =>
            {
                try
                {
                    var (isHealthy, reason) = await check.CheckHealthAsync(DependencyTimeout, overallCts.Token).ConfigureAwait(false);
                    return new DependencyHealth
                    {
                        Name = check.DependencyName,
                        Status = isHealthy ? "Healthy" : "Unhealthy",
                        Reason = reason
                    };
                }
                catch (OperationCanceledException)
                {
                    return new DependencyHealth
                    {
                        Name = check.DependencyName,
                        Status = "Unhealthy",
                        Reason = "Connection timeout"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Health check failed for dependency {DependencyName}", check.DependencyName);
                    return new DependencyHealth
                    {
                        Name = check.DependencyName,
                        Status = "Unhealthy",
                        Reason = ex.Message
                    };
                }
            });

            var results = await Task.WhenAll(checkTasks).ConfigureAwait(false);
            dependencies.AddRange(results);
            allHealthy = dependencies.All(d => d.Status == "Healthy");
        }
        catch (OperationCanceledException)
        {
            // Overall timeout exceeded
            _logger.LogWarning("Readiness check timed out after {Timeout}s", OverallTimeout.TotalSeconds);

            // Mark any unchecked dependencies as unhealthy
            var checkedNames = dependencies.Select(d => d.Name).ToHashSet();
            foreach (var check in _dependencyChecks)
            {
                if (!checkedNames.Contains(check.DependencyName))
                {
                    dependencies.Add(new DependencyHealth
                    {
                        Name = check.DependencyName,
                        Status = "Unhealthy",
                        Reason = "Connection timeout"
                    });
                }
            }
            allHealthy = false;
        }

        var response = new HealthCheckResponse
        {
            Status = allHealthy ? "Healthy" : "Unhealthy",
            Dependencies = dependencies
        };

        if (allHealthy)
        {
            return Ok(response);
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}
