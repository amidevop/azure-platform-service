using System.Net;
using System.Net.Http.Json;
using AzurePlatformService.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using FluentAssertions;

namespace AzurePlatformService.Tests.Unit.Api;

public class HealthCheckTests : IClassFixture<WebApplicationFactory<AzurePlatformService.Api.Program>>
{
    private readonly HttpClient _client;

    public HealthCheckTests(WebApplicationFactory<AzurePlatformService.Api.Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LiveEndpoint_ReturnsOk_WithHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("Healthy");
    }

    [Fact]
    public async Task LiveEndpoint_ReturnsJsonContentType()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task LiveEndpoint_RespondsWithinTimeout()
    {
        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await _client.GetAsync("/health/live", cts.Token);

        // Assert - if we reach here, the response completed within the timeout
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
