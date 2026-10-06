using Mersad.Api.Models;
using Mersad.Api.Detection.Policies;

namespace Mersad.Api.Detection.Rules;

public class SuspiciousNetworkPortRule : IDetectionRule
{
    public string RuleId => "NET-001";

    public IEnumerable<Detection> Evaluate(
        DetectionContext context)
    {
        foreach (var networkEvent in context.NetworkEvents)
        {
            if (!SuspiciousNetworkPorts.Contains(networkEvent.RemotePort))
            {
                continue;
            }

            yield return new Detection
            {
                RuleId = RuleId,
                Title = "Suspicious Network Connection",
                Description =
                    $"A network connection to suspicious remote port " +
                    $"{networkEvent.RemotePort} was detected.",
                Severity = "High",
                DeviceId = networkEvent.DeviceId,
                NetworkEventId = networkEvent.Id,
                DetectedAt = DateTimeOffset.UtcNow
            };
        }
    }
}