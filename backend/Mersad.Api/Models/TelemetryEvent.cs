namespace Mersad.Api.Models;

public class TelemetryEvent
{
    public string Type { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    public Dictionary<string, object> Data { get; set; }
        = new();
}