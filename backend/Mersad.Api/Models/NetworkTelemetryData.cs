namespace Mersad.Api.Models;

public class NetworkTelemetryData
{
    public int ProcessId { get; set; }
    public string LocalAddress { get; set; } = string.Empty;
    public int LocalPort { get; set; }
    public string RemoteAddress { get; set; } = string.Empty;
    public int RemotePort { get; set; }
    public string State { get; set; } = string.Empty;
}
