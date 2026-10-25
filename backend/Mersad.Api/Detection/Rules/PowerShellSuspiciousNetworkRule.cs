
using Mersad.Api.Configuration;
using Mersad.Api.Models;
using Microsoft.Extensions.Options;

namespace Mersad.Api.Detection.Rules;

public class PowerShellSuspiciousNetworkRule : IDetectionRule
{
    private static readonly TimeSpan CorrelationWindow =
        TimeSpan.FromMinutes(5);

    private readonly DetectionOptions _options;
    private readonly HashSet<int> _suspiciousPorts;

    public PowerShellSuspiciousNetworkRule(
        IOptions<DetectionOptions> options)
    {
        _options = options.Value;
        _suspiciousPorts =
            _options.SuspiciousNetworkPorts.ToHashSet();
    }

    public string RuleId => "CORR-001";

    public IEnumerable<Detection> Evaluate(
        DetectionContext context)
    {
        if (!_options.PowerShellSuspiciousNetworkEnabled)
        {
            yield break;
        }

        var powershellProcesses = context.ProcessEvents
            .Where(processEvent =>
                processEvent.ProcessName.Equals(
                    "powershell.exe",
                    StringComparison.OrdinalIgnoreCase));

        foreach (var processEvent in powershellProcesses)
        {
            foreach (var networkEvent in context.NetworkEvents)
            {
                // A process ID is only meaningful within its device.
                if (networkEvent.DeviceId != processEvent.DeviceId ||
                    networkEvent.ProcessId != processEvent.ProcessId)
                {
                    continue;
                }

                if (!_suspiciousPorts.Contains(
                        networkEvent.RemotePort))
                {
                    continue;
                }

                var timeDifference =
                    (processEvent.Timestamp -
                     networkEvent.Timestamp).Duration();

                if (timeDifference > CorrelationWindow)
                {
                    continue;
                }

                yield return new Detection
                {
                    RuleId = RuleId,
                    Title = "PowerShell Suspicious Network Activity",
                    Description =
                        "PowerShell was associated with a network event " +
                        "to a suspicious remote port within five minutes.",
                    Severity = "High",
                    DeviceId = processEvent.DeviceId,
                    ProcessEventId = processEvent.Id,
                    NetworkEventId = networkEvent.Id,
                    DetectedAt = DateTimeOffset.UtcNow
                };
            }
        }
    }
}
