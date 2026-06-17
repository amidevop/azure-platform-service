using System.ComponentModel.DataAnnotations;

namespace AzurePlatformService.Shared.Models;

public record WorkItemRequest
{
    [Required]
    [MinLength(1)]
    public string Payload { get; init; } = string.Empty;
}
