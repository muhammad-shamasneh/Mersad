namespace Mersad.Api.Models;

public class ProcessEvent
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}