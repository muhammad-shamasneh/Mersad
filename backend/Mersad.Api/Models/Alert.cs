namespace Mersad.Api.Models;

public class Alert
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public string DetectionRuleId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Status { get; set; } = "New";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid? ProcessEventId { get; set; }

    public Guid? NetworkEventId { get; set; }
}