
using Mersad.Api.Configuration;
using Mersad.Api.Models;
using Microsoft.Extensions.Options;

namespace Mersad.Api.Detection.Rules;

public class PowerShellSuspiciousNetworkRule : IDetectionRule
{
    private readonly DetectionOptions _options;
    private readonly HashSet<int> _suspiciousPorts;

    public PowerShellSuspiciousNetworkRule(
        IOptions<DetectionOptions> options)
    {
        _options = options.Value;
        _suspiciousPorts = _options.SuspiciousNetworkPorts.ToHashSet();
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
                if (networkEvent.ProcessId != processEvent.ProcessId)
                {
                    continue;
                }

                if (!_suspiciousPorts.Contains(networkEvent.RemotePort))
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