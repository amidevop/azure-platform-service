using System.Text.Json;
using AzurePlatformService.Api.Services;
using AzurePlatformService.Shared.Models;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;

namespace AzurePlatformService.Tests.Property.Api;

/// <summary>
/// Property-based tests for work item retrieval ordering, pagination, and serialization.
/// </summary>
[Trait("Feature", "azure-platform-service")]
public class WorkItemRetrievalProperties
{
    /// <summary>
    /// Property 4: Processed work items are returned in reverse chronological order.
    /// For any collection of processed work items with random timestamps,
    /// GetRecentAsync SHALL return them ordered by ProcessedAt descending (most recent first)
    /// and limited to at most 100 items.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(Arbitrary = new[] { typeof(WorkItemArbitrary) })]
    public async Task<bool> GetRecentAsync_ReturnsItemsInReverseChronologicalOrder(List<WorkItem> workItems)
    {
        // Arrange
        var store = new InMemoryWorkItemStore();
        foreach (var item in workItems)
        {
            await store.AddAsync(item);
        }

        // Act
        var result = await store.GetRecentAsync(100);

        // Assert - Items are ordered by ProcessedAt descending
        for (int i = 0; i < result.Count - 1; i++)
        {
            if (result[i].ProcessedAt < result[i + 1].ProcessedAt)
            {
                return false;
            }
        }

        // Assert - Result is capped at 100 items
        if (result.Count > 100)
        {
            return false;
        }

        // Assert - Result count is min(workItems.Count, 100)
        var expectedCount = Math.Min(workItems.Count, 100);
        if (result.Count != expectedCount)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Property 4 (supplemental): When more than 100 items exist, only the 100 most recent are returned.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property]
    public async Task<bool> GetRecentAsync_CapsResultAt100Items(PositiveInt extraCount)
    {
        // Arrange - Create more than 100 items
        var totalCount = 100 + (extraCount.Get % 50) + 1; // Between 101 and 151 items
        var store = new InMemoryWorkItemStore();
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < totalCount; i++)
        {
            var item = new WorkItem
            {
                Id = Guid.NewGuid(),
                Payload = $"payload-{i}",
                ProcessedAt = baseTime.AddMinutes(i),
                Status = WorkItemStatus.Completed
            };
            await store.AddAsync(item);
        }

        // Act
        var result = await store.GetRecentAsync(100);

        // Assert - Exactly 100 items returned
        if (result.Count != 100)
        {
            return false;
        }

        // Assert - The returned items are the most recent ones (highest ProcessedAt values)
        var oldestReturned = result.Last().ProcessedAt;
        var expectedOldestTime = baseTime.AddMinutes(totalCount - 100);
        if (oldestReturned != expectedOldestTime)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Property 5: Work item serialization includes all required fields.
    /// For any valid WorkItem instance, the JSON serialization SHALL include the fields:
    /// id (non-empty GUID), payload (original content), processedAt (timestamp), and status (valid enum value).
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(Arbitrary = new[] { typeof(CompletedWorkItemArbitrary) })]
    public void WorkItem_JsonSerialization_IncludesAllRequiredFields(WorkItem workItem)
    {
        // Arrange
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(workItem, options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert - id field is present and is a valid non-empty GUID
        root.TryGetProperty("id", out var idElement).Should().BeTrue("JSON must include 'id' field");
        var idValue = idElement.GetString();
        idValue.Should().NotBeNullOrEmpty();
        Guid.TryParse(idValue, out var parsedId).Should().BeTrue("id must be a valid GUID");
        parsedId.Should().NotBe(Guid.Empty, "id must not be an empty GUID");
        parsedId.Should().Be(workItem.Id);

        // Assert - payload field is present and matches original content
        root.TryGetProperty("payload", out var payloadElement).Should().BeTrue("JSON must include 'payload' field");
        payloadElement.GetString().Should().Be(workItem.Payload);

        // Assert - processedAt field is present
        root.TryGetProperty("processedAt", out var processedAtElement).Should().BeTrue("JSON must include 'processedAt' field");
        processedAtElement.ValueKind.Should().NotBe(JsonValueKind.Undefined);

        // Assert - status field is present and is a valid enum value
        root.TryGetProperty("status", out var statusElement).Should().BeTrue("JSON must include 'status' field");
        var statusValue = statusElement.GetInt32();
        Enum.IsDefined(typeof(WorkItemStatus), statusValue).Should().BeTrue("status must be a valid WorkItemStatus value");
    }
}

/// <summary>
/// Custom arbitrary for generating WorkItem instances with non-null ProcessedAt timestamps.
/// </summary>
public class WorkItemArbitrary
{
    public static Arbitrary<List<WorkItem>> WorkItemList()
    {
        var workItemGen = from id in Arb.Generate<Guid>().Where(g => g != Guid.Empty)
                          from payload in Arb.Generate<NonEmptyString>()
                          from year in Gen.Choose(2020, 2025)
                          from month in Gen.Choose(1, 12)
                          from day in Gen.Choose(1, 28)
                          from hour in Gen.Choose(0, 23)
                          from minute in Gen.Choose(0, 59)
                          from second in Gen.Choose(0, 59)
                          from status in Gen.Elements(
                              WorkItemStatus.Completed,
                              WorkItemStatus.Failed,
                              WorkItemStatus.Processing,
                              WorkItemStatus.Pending)
                          select new WorkItem
                          {
                              Id = id,
                              Payload = payload.Get,
                              ProcessedAt = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc),
                              Status = status
                          };

        var listGen = from count in Gen.Choose(0, 150)
                      from items in Gen.ListOf(count, workItemGen)
                      select items.ToList();

        return Arb.From(listGen);
    }
}

/// <summary>
/// Custom arbitrary for generating valid WorkItem instances with all fields populated.
/// Used for serialization completeness testing.
/// </summary>
public class CompletedWorkItemArbitrary
{
    public static Arbitrary<WorkItem> WorkItem()
    {
        var gen = from id in Arb.Generate<Guid>().Where(g => g != Guid.Empty)
                  from payload in Arb.Generate<NonEmptyString>()
                  from year in Gen.Choose(2020, 2025)
                  from month in Gen.Choose(1, 12)
                  from day in Gen.Choose(1, 28)
                  from hour in Gen.Choose(0, 23)
                  from minute in Gen.Choose(0, 59)
                  from second in Gen.Choose(0, 59)
                  from status in Gen.Elements(
                      WorkItemStatus.Completed,
                      WorkItemStatus.Failed,
                      WorkItemStatus.Processing,
                      WorkItemStatus.Pending)
                  select new Shared.Models.WorkItem
                  {
                      Id = id,
                      Payload = payload.Get,
                      ProcessedAt = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc),
                      Status = status
                  };

        return Arb.From(gen);
    }
}
