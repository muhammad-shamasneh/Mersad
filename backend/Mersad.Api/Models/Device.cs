namespace Mersad.Api.Models;

public class Device
{
    public Guid Id { get; set; }

    public Guid AgentId { get; set; }

    public string Hostname { get; set; } = string.Empty;
    public string OS { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; }

    public ICollection<ProcessEvent> ProcessEvents { get; set; }
        = new List<ProcessEvent>();
}