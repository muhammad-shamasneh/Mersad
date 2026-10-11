
namespace Mersad.Api.Configuration;

public sealed class DetectionOptions
{
    public const string SectionName = "Detection";

    public bool SuspiciousPowerShellEnabled { get; set; } = true;

    public bool SuspiciousNetworkPortEnabled { get; set; } = true;

    public bool PowerShellSuspiciousNetworkEnabled { get; set; } = true;

    public List<int> SuspiciousNetworkPorts { get; set; } =
    [
        4444,
        1337,
        31337
    ];

    public bool IsValid()
    {
        return SuspiciousNetworkPorts is not null
            && SuspiciousNetworkPorts.All(
                port => port is >= 1 and <= 65535)
            && SuspiciousNetworkPorts.Distinct().Count()
                == SuspiciousNetworkPorts.Count;
    }
}