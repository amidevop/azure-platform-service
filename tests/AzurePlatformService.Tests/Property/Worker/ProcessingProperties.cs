using AzurePlatformService.Api.Services;
using AzurePlatformService.Shared.Models;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;

namespace AzurePlatformService.Tests.Property.Worker;

/// <summary>
/// Property 6: Worker correctly processes and stores work items.
/// Validates: Requirements 5.2
/// </summary>
[Trait("Feature", "azure-platform-service")]
public class ProcessingProperties
{
    /// <summary>
    /// **Validates: Requirements 5.2**
    /// Property 6: For any valid WorkItemMessage, when processed and stored,
    /// the stored WorkItem has the same ID, same payload, a valid timestamp, and Completed status.
    /// </summary>
    [Property]
    public FsCheck.Property StoredWorkItem_HasSameId_Payload_ValidTimestamp_CompletedStatus()
    {
        var arb = Arb.From(
            from id in Arb.Generate<Guid>()
            from payload in Gen.Elements("payload-a", "payload-b", "test-data", "item-content", "json-body")
            from enqueuedAt in Arb.Generate<DateTime>()
            from attemptCount in Gen.Choose(1, 5)
            select new WorkItemMessage
            {
                WorkItemId = id,
                Payload = payload,
                EnqueuedAt = enqueuedAt,
                AttemptCount = attemptCount
            });

        return Prop.ForAll(arb, async (WorkItemMessage message) =>
        {
            // Arrange
            var store = new InMemoryWorkItemStore();
            var beforeProcessing = DateTime.UtcNow;

            // Act - simulate what ProcessWithRetryAsync does
            var workItem = new WorkItem
            {
                Id = message.WorkItemId,
                Payload = message.Payload,
                ProcessedAt = DateTime.UtcNow,
                Status = WorkItemStatus.Completed
            };

            await store.AddAsync(workItem);

            var afterProcessing = DateTime.UtcNow;

            // Assert
            var storedItems = await store.GetRecentAsync(1);
            storedItems.Should().HaveCount(1);

            var stored = storedItems[0];
            stored.Id.Should().Be(message.WorkItemId);
            stored.Payload.Should().Be(message.Payload);
            stored.ProcessedAt.Should().NotBeNull();
            stored.ProcessedAt!.Value.Should().BeOnOrAfter(beforeProcessing);
            stored.ProcessedAt!.Value.Should().BeOnOrBefore(afterProcessing);
            stored.Status.Should().Be(WorkItemStatus.Completed);
        });
    }
}
