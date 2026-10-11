
using Mersad.Api.Configuration;
using Mersad.Api.Models;
using Microsoft.Extensions.Options;

namespace Mersad.Api.Detection.Rules;

public class SuspiciousNetworkPortRule : IDetectionRule
{
    private readonly DetectionOptions _options;
    private readonly HashSet<int> _suspiciousPorts;

    public SuspiciousNetworkPortRule(
        IOptions<DetectionOptions> options)
    {
        _options = options.Value;
        _suspiciousPorts = _options.SuspiciousNetworkPorts.ToHashSet();
    }

    public string RuleId => "NET-001";

    public IEnumerable<Detection> Evaluate(
        DetectionContext context)
    {
        if (!_options.SuspiciousNetworkPortEnabled)
        {
            yield break;
        }

        foreach (var networkEvent in context.NetworkEvents)
        {
            if (!_suspiciousPorts.Contains(networkEvent.RemotePort))
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