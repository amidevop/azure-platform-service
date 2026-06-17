using AzurePlatformService.Shared.Interfaces;

namespace AzurePlatformService.Api.HealthChecks;

/// <summary>
/// Checks Application Insights connectivity by verifying the telemetry client is configured.
/// </summary>
public class ApplicationInsightsHealthCheck : IDependencyHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public string DependencyName => "ApplicationInsights";

    public ApplicationInsightsHealthCheck(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient("ApplicationInsightsHealthCheck");
    }

    public async Task<(bool IsHealthy, string? Reason)> CheckHealthAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            var connectionString = _configuration.GetValue<string>("ApplicationInsights:ConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                return (false, "Connection string not configured");
            }

            // Parse the ingestion endpoint from connection string
            var parts = connectionString.Split(';')
                .Select(p => p.Split('=', 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

            if (!parts.TryGetValue("IngestionEndpoint", out var ingestionEndpoint))
            {
                return (false, "Ingestion endpoint not found in connection string");
            }

            // Perform a lightweight connectivity check to the ingestion endpoint
            var request = new HttpRequestMessage(HttpMethod.Get, ingestionEndpoint);
            var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);

            // Any response (even 4xx) means the service is reachable
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
