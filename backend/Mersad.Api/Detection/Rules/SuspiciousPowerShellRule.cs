
using Mersad.Api.Configuration;
using Mersad.Api.Models;
using Microsoft.Extensions.Options;

namespace Mersad.Api.Detection.Rules;

public class SuspiciousPowerShellRule : IDetectionRule
{
    private readonly DetectionOptions _options;

    public SuspiciousPowerShellRule(
        IOptions<DetectionOptions> options)
    {
        _options = options.Value;
    }

    public string RuleId => "PROC-001";

    public IEnumerable<Detection> Evaluate(
        DetectionContext context)
    {
        if (!_options.SuspiciousPowerShellEnabled)
        {
            yield break;
        }

        foreach (var processEvent in context.ProcessEvents)
        {
            if (!processEvent.ProcessName.Equals(
                    "powershell.exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new Detection
            {
                RuleId = RuleId,
                Title = "Suspicious PowerShell Execution",
                Description =
                    "PowerShell process execution was detected.",
                Severity = "Medium",
                DeviceId = processEvent.DeviceId,
                ProcessEventId = processEvent.Id,
                DetectedAt = DateTimeOffset.UtcNow
            };
        }
    }
}