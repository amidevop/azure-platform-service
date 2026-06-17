using System.Net;
using System.Net.Http.Json;
using AzurePlatformService.Shared.Interfaces;
using AzurePlatformService.Shared.Models;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AzurePlatformService.Tests.Property.Api;

/// <summary>
/// Property-based tests for the readiness probe endpoint (/health/ready).
/// Validates: Requirements 2.1, 2.2
/// </summary>
[Trait("Feature", "azure-platform-service")]
public class ReadinessProbeProperties
{
    /// <summary>
    /// Property 1: Readiness probe correctly identifies unhealthy dependencies.
    /// For any set of dependency health states, the probe correctly lists unhealthy ones,
    /// returning 503 if any dependency is unhealthy and 200 only if all are healthy.
    /// **Validates: Requirements 2.1, 2.2**
    /// </summary>
    [Property(Arbitrary = new[] { typeof(DependencyHealthArbitrary) })]
    public async Task<bool> Readiness_probe_correctly_identifies_unhealthy_dependencies(
        List<DependencyHealthState> dependencyStates)
    {
        if (dependencyStates == null || dependencyStates.Count == 0)
            return true; // Vacuously true for empty input

        // Arrange: Create mocks for each dependency
        var mocks = dependencyStates.Select(state =>
        {
            var mock = new Mock<IDependencyHealthCheck>();
            mock.Setup(x => x.DependencyName).Returns(state.Name);
            mock.Setup(x => x.CheckHealthAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((state.IsHealthy, state.Reason));
            return mock;
        }).ToList();

        await using var factory = new WebApplicationFactory<AzurePlatformService.Api.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing IDependencyHealthCheck registrations
                    var descriptors = services
                        .Where(d => d.ServiceType == typeof(IDependencyHealthCheck))
                        .ToList();
                    foreach (var descriptor in descriptors)
                    {
                        services.Remove(descriptor);
                    }

                    // Add our mocked dependencies
                    foreach (var mock in mocks)
                    {
                        services.AddSingleton<IDependencyHealthCheck>(mock.Object);
                    }
                });
            });

        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();

        // Assert
        var anyUnhealthy = dependencyStates.Any(d => !d.IsHealthy);
        var expectedStatusCode = anyUnhealthy ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;

        if (response.StatusCode != expectedStatusCode)
            return false;

        if (body == null)
            return false;

        // Verify overall status matches
        var expectedOverallStatus = anyUnhealthy ? "Unhealthy" : "Healthy";
        if (body.Status != expectedOverallStatus)
            return false;

        // Verify each unhealthy dependency is listed
        var unhealthyDeps = dependencyStates.Where(d => !d.IsHealthy).ToList();
        if (body.Dependencies == null && unhealthyDeps.Count > 0)
            return false;

        if (body.Dependencies != null)
        {
            foreach (var unhealthy in unhealthyDeps)
            {
                var found = body.Dependencies.FirstOrDefault(d => d.Name == unhealthy.Name);
                if (found == null || found.Status != "Unhealthy")
                    return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Represents a dependency health state for property-based testing.
/// </summary>
public record DependencyHealthState
{
    public string Name { get; init; } = string.Empty;
    public bool IsHealthy { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// FsCheck arbitrary generator for dependency health states.
/// </summary>
public static class DependencyHealthArbitrary
{
    public static Arbitrary<List<DependencyHealthState>> ArbitraryDependencyStates()
    {
        var nameGen = Gen.Elements(
            "ServiceBus", "ApplicationInsights", "Database", "Cache", "Storage");

        var reasonGen = Gen.Elements(
            "Connection timeout",
            "Authentication failed",
            "Service unavailable",
            "Network error",
            (string?)null);

        var stateGen = from name in nameGen
                       from isHealthy in Arb.Generate<bool>()
                       from reason in reasonGen
                       select new DependencyHealthState
                       {
                           Name = name,
                           IsHealthy = isHealthy,
                           Reason = isHealthy ? null : reason
                       };

        // Generate 1-5 dependencies with unique names
        var listGen = from count in Gen.Choose(1, 5)
                      from states in Gen.ListOf(count, stateGen)
                      select states
                          .GroupBy(s => s.Name)
                          .Select(g => g.First())
                          .ToList();

        return Arb.From(listGen);
    }
}
