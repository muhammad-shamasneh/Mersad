using Mersad.Api.Models;

namespace Mersad.Api.Detection;

public class Detection
{
    public string RuleId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public Guid DeviceId { get; set; }

    public Guid? ProcessEventId { get; set; }

    public Guid? NetworkEventId { get; set; }

    public DateTimeOffset DetectedAt { get; set; }
}