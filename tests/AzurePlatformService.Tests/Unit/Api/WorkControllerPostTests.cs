using System.Net;
using System.Net.Http.Json;
using AzurePlatformService.Api.Services;
using AzurePlatformService.Shared.Interfaces;
using AzurePlatformService.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using FluentAssertions;
using Azure.Messaging.ServiceBus;

namespace AzurePlatformService.Tests.Unit.Api;

public class WorkControllerPostTests : IClassFixture<WebApplicationFactory<AzurePlatformService.Api.Program>>
{
    private readonly WebApplicationFactory<AzurePlatformService.Api.Program> _factory;

    public WorkControllerPostTests(WebApplicationFactory<AzurePlatformService.Api.Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithMocks(Mock<IMessagePublisher>? publisherMock = null)
    {
        var mock = publisherMock ?? new Mock<IMessagePublisher>();
        mock.Setup(p => p.PublishAsync(It.IsAny<WorkItemMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing IMessagePublisher registration if any
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMessagePublisher));
                if (descriptor != null) services.Remove(descriptor);

                // Remove ServiceBusSender registration if any
                var senderDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ServiceBusSender));
                if (senderDescriptor != null) services.Remove(senderDescriptor);

                services.AddSingleton(mock.Object);

                // Ensure CircuitBreakerService is registered
                var cbDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(CircuitBreakerService));
                if (cbDescriptor == null)
                {
                    services.AddSingleton<CircuitBreakerService>();
                }
            });
        }).CreateClient();
    }

    [Fact]
    public async Task PostWorkItem_ValidPayload_Returns202Accepted()
    {
        // Arrange
        var client = CreateClientWithMocks();
        var request = new WorkItemRequest { Payload = "test work item payload" };

        // Act
        var response = await client.PostAsJsonAsync("/api/work", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostWorkItem_ValidPayload_ReturnsIdInResponse()
    {
        // Arrange
        var client = CreateClientWithMocks();
        var request = new WorkItemRequest { Payload = "test work item" };

        // Act
        var response = await client.PostAsJsonAsync("/api/work", request);
        var body = await response.Content.ReadFromJsonAsync<PostWorkItemResponse>();

        // Assert
        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();
        body.Status.Should().Be("Accepted");
    }

    [Fact]
    public async Task PostWorkItem_ValidPayload_PublishesMessageToServiceBus()
    {
        // Arrange
        var publisherMock = new Mock<IMessagePublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<WorkItemMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var client = CreateClientWithMocks(publisherMock);
        var request = new WorkItemRequest { Payload = "enqueue this" };

        // Act
        await client.PostAsJsonAsync("/api/work", request);

        // Assert
        publisherMock.Verify(
            p => p.PublishAsync(
                It.Is<WorkItemMessage>(m => m.Payload == "enqueue this" && m.AttemptCount == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PostWorkItem_NullBody_Returns400BadRequest()
    {
        // Arrange
        var client = CreateClientWithMocks();

        // Act - send null JSON
        var response = await client.PostAsync("/api/work",
            new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostWorkItem_EmptyPayload_Returns400BadRequest()
    {
        // Arrange
        var client = CreateClientWithMocks();
        var request = new WorkItemRequest { Payload = "" };

        // Act
        var response = await client.PostAsJsonAsync("/api/work", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostWorkItem_WhitespacePayload_Returns400BadRequest()
    {
        // Arrange
        var client = CreateClientWithMocks();
        var request = new WorkItemRequest { Payload = "   " };

        // Act
        var response = await client.PostAsJsonAsync("/api/work", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostWorkItem_ServiceBusUnavailable_Returns503()
    {
        // Arrange
        var publisherMock = new Mock<IMessagePublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<WorkItemMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceBusException("Connection refused", ServiceBusFailureReason.ServiceCommunicationProblem));

        var client = CreateClientWithMocks(publisherMock);
        var request = new WorkItemRequest { Payload = "test payload" };

        // Act
        var response = await client.PostAsJsonAsync("/api/work", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task PostWorkItem_CircuitBreakerOpen_Returns503()
    {
        // Arrange
        var publisherMock = new Mock<IMessagePublisher>();
        publisherMock.Setup(p => p.PublishAsync(It.IsAny<WorkItemMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CircuitBreakerOpenException("Circuit breaker is open"));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMessagePublisher));
                if (descriptor != null) services.Remove(descriptor);

                var senderDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ServiceBusSender));
                if (senderDescriptor != null) services.Remove(senderDescriptor);

                services.AddSingleton(publisherMock.Object);

                // Use a circuit breaker that's already in open state
                var cbDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(CircuitBreakerService));
                if (cbDescriptor != null) services.Remove(cbDescriptor);

                // Create a circuit breaker and open it by triggering 5 failures
                var cb = new CircuitBreakerService();
                services.AddSingleton(cb);
            });
        }).CreateClient();

        // We need to trigger the circuit breaker to open state first
        // Since the publisher throws CircuitBreakerOpenException directly won't work
        // because the circuit breaker wraps the call. Let's use a different approach:
        // The ServiceBusException will be caught by the circuit breaker and count as failures.
        // After 5 failures, it will open. But for this test, we want to test
        // CircuitBreakerOpenException being caught by the controller.
        // The circuit breaker itself throws CircuitBreakerOpenException, so we need
        // to force it open. Let's trigger 5 failures first.

        var failPublisher = new Mock<IMessagePublisher>();
        failPublisher.Setup(p => p.PublishAsync(It.IsAny<WorkItemMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceBusException("fail", ServiceBusFailureReason.ServiceCommunicationProblem));

        var failClient = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMessagePublisher));
                if (descriptor != null) services.Remove(descriptor);

                var senderDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ServiceBusSender));
                if (senderDescriptor != null) services.Remove(senderDescriptor);

                services.AddSingleton(failPublisher.Object);

                var cbDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(CircuitBreakerService));
                if (cbDescriptor == null)
                {
                    services.AddSingleton<CircuitBreakerService>();
                }
            });
        }).CreateClient();

        var request = new WorkItemRequest { Payload = "test payload" };

        // Trigger 5 failures to open the circuit breaker
        for (int i = 0; i < 5; i++)
        {
            await failClient.PostAsJsonAsync("/api/work", request);
        }

        // The 6th call should get a 503 due to circuit breaker being open
        var response = await failClient.PostAsJsonAsync("/api/work", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private record PostWorkItemResponse(Guid Id, string Status);
}
