namespace Mersad.Api.Models;

public class TelemetryRequest
{
    public Guid AgentId { get; set; }

    public List<TelemetryEvent> Events { get; set; }
        = new();
}