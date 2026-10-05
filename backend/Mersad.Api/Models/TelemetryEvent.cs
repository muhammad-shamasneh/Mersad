using System.Text.Json;

namespace Mersad.Api.Models;

public class TelemetryEvent
{
    public string Type { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    public JsonElement Data { get; set; }
}