using System.Text.Json;
using Azure.Messaging.ServiceBus;
using AzurePlatformService.Shared.Interfaces;
using AzurePlatformService.Shared.Models;
using AzurePlatformService.Worker;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AzurePlatformService.Tests.Unit.Worker;

public class WorkerServiceTests
{
    [Theory]
    [InlineData("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", true)]
    [InlineData("00-abcdef1234567890abcdef1234567890-1234567890abcdef-00", true)]
    [InlineData("", false)]
    [InlineData("invalid", false)]
    [InlineData("00-short-short-01", false)]
    [InlineData("01-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", false)] // wrong version
    [InlineData("00-00000000000000000000000000000000-b7ad6b7169203331-01", false)] // all-zero trace id
    [InlineData("00-0af7651916cd43dd8448eb211c80319c-0000000000000000-01", false)] // all-zero span id
    [InlineData("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331", false)] // missing flags
    [InlineData("00-GHIJK51916cd43dd8448eb211c80319c-b7ad6b7169203331-01", false)] // non-hex chars
    public void IsValidTraceparent_ClassifiesCorrectly(string traceparent, bool expected)
    {
        WorkerService.IsValidTraceparent(traceparent).Should().Be(expected);
    }

    [Theory]
    [InlineData(typeof(TimeoutException), true)]
    [InlineData(typeof(HttpRequestException), true)]
    [InlineData(typeof(OperationCanceledException), true)]
    [InlineData(typeof(JsonException), false)]
    [InlineData(typeof(FormatException), false)]
    [InlineData(typeof(InvalidOperationException), false)]
    [InlineData(typeof(ArgumentException), false)]
    [InlineData(typeof(NullReferenceException), false)]
    public void IsTransientError_ClassifiesCorrectly(Type exceptionType, bool expected)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType, "test")!;
        WorkerService.IsTransientError(ex).Should().Be(expected);
    }

    [Theory]
    [InlineData(typeof(JsonException), true)]
    [InlineData(typeof(FormatException), true)]
    [InlineData(typeof(InvalidOperationException), true)]
    [InlineData(typeof(ArgumentException), true)]
    [InlineData(typeof(TimeoutException), false)]
    [InlineData(typeof(HttpRequestException), false)]
    [InlineData(typeof(NullReferenceException), false)]
    public void IsNonTransientError_ClassifiesCorrectly(Type exceptionType, bool expected)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType, "test")!;
        WorkerService.IsNonTransientError(ex).Should().Be(expected);
    }
}
