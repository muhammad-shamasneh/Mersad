namespace Mersad.Api.Models;

public class NetworkEvent
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public int ProcessId { get; set; }
    public string LocalAddress { get; set; } = string.Empty;
    public int LocalPort { get; set; }
    public string RemoteAddress { get; set; } = string.Empty;
    public int RemotePort { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}
