using System.Text.Json;
using AzurePlatformService.Worker;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;

namespace AzurePlatformService.Tests.Property.Worker;

/// <summary>
/// Property 8: Non-transient errors bypass retry and dead-letter immediately.
/// Property 13: Worker creates root span for missing trace context.
/// Validates: Requirements 12.6, 17.4
/// </summary>
[Trait("Feature", "azure-platform-service")]
public class ErrorClassificationProperties
{
    /// <summary>
    /// **Validates: Requirements 12.6**
    /// Property 8: Non-transient errors are correctly identified - they bypass retry
    /// and should be dead-lettered immediately. IsNonTransientError returns true
    /// and IsTransientError returns false for these exceptions.
    /// </summary>
    [Property]
    public FsCheck.Property NonTransientErrors_AreClassifiedCorrectly_AndBypassRetry()
    {
        var nonTransientExceptions = Gen.Elements<Exception>(
            new JsonException("Invalid JSON"),
            new FormatException("Bad format"),
            new InvalidOperationException("Invalid operation"),
            new ArgumentException("Bad argument"),
            new ArgumentNullException("param"),
            new JsonException("Unexpected token"),
            new FormatException("Input string was not in a correct format"),
            new InvalidOperationException("Sequence contains no elements"),
            new ArgumentException("Value does not fall within the expected range")
        );

        var arb = Arb.From(nonTransientExceptions);

        return Prop.ForAll(arb, (Exception ex) =>
        {
            // Act
            var isNonTransient = WorkerService.IsNonTransientError(ex);
            var isTransient = WorkerService.IsTransientError(ex);

            // Assert - non-transient errors should be identified as such
            isNonTransient.Should().BeTrue(
                because: $"{ex.GetType().Name} is a non-transient error that should bypass retry");

            // Non-transient errors should NOT be classified as transient
            isTransient.Should().BeFalse(
                because: $"{ex.GetType().Name} should not be retried");
        });
    }

    /// <summary>
    /// **Validates: Requirements 17.4**
    /// Property 13: Missing or malformed traceparent values are correctly identified
    /// as invalid, meaning the worker would create a root span with a warning attribute.
    /// IsValidTraceparent returns false for missing/malformed values.
    /// </summary>
    [Property]
    public FsCheck.Property MissingOrMalformedTraceparent_IsIdentifiedAsInvalid()
    {
        var malformedTraceparents = Gen.Elements(
            // Missing version prefix
            "ab4a9f658af7ee8000000000000000000-b7ad6b7169203331-01",
            // Wrong version
            "01-ab4a9f658af7ee8000000000000000000-b7ad6b7169203331-01",
            // TraceId too short
            "00-ab4a9f658af7ee80-b7ad6b7169203331-01",
            // SpanId too short
            "00-ab4a9f658af7ee8000000000000000ab-b7ad6b-01",
            // Non-hex characters in traceId
            "00-zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz-b7ad6b7169203331-01",
            // Non-hex characters in spanId
            "00-ab4a9f658af7ee8000000000000000ab-zzzzzzzzzzzzzzzz-01",
            // Empty string
            "",
            // Random garbage
            "not-a-traceparent",
            // Missing parts
            "00-ab4a9f658af7ee8000000000000000ab",
            // All zeros traceId (invalid per W3C spec)
            "00-00000000000000000000000000000000-b7ad6b7169203331-01",
            // All zeros spanId (invalid per W3C spec)
            "00-ab4a9f658af7ee8000000000000000ab-0000000000000000-01",
            // Too many parts
            "00-ab4a9f658af7ee8000000000000000ab-b7ad6b7169203331-01-extra",
            // Flags too long
            "00-ab4a9f658af7ee8000000000000000ab-b7ad6b7169203331-0100"
        );

        var arb = Arb.From(malformedTraceparents);

        return Prop.ForAll(arb, (string traceparent) =>
        {
            // Act
            var isValid = WorkerService.IsValidTraceparent(traceparent);

            // Assert - malformed traceparent should be invalid,
            // which means worker would create a root span with warning attribute
            isValid.Should().BeFalse(
                because: $"'{traceparent}' is malformed and should trigger root span creation with warning");
        });
    }
}
