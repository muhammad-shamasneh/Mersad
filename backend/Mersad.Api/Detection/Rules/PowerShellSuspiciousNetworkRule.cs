using Mersad.Api.Models;
using Mersad.Api.Detection.Policies;

namespace Mersad.Api.Detection.Rules;

public class PowerShellSuspiciousNetworkRule : IDetectionRule
{
    public string RuleId => "CORR-001";

    public IEnumerable<Detection> Evaluate(
        DetectionContext context)
    {
        var powershellProcesses = context.ProcessEvents
            .Where(processEvent =>
                processEvent.ProcessName.Equals(
                    "powershell.exe",
                    StringComparison.OrdinalIgnoreCase));

        foreach (var processEvent in powershellProcesses)
        {
            foreach (var networkEvent in context.NetworkEvents)
            {
                if (networkEvent.ProcessId != processEvent.ProcessId)
                {
                    continue;
                }

                if (!SuspiciousNetworkPorts.Contains(networkEvent.RemotePort))
                {
                    continue;
                }

                yield return new Detection
                {
                    RuleId = RuleId,
                    Title = "PowerShell Suspicious Network Activity",
                    Description =
                        "PowerShell established a network connection to a suspicious remote port.",
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