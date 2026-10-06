using Mersad.Api.Models;

namespace Mersad.Api.Detection.Rules;

public class SuspiciousPowerShellRule : IDetectionRule
{
    public string RuleId => "PROC-001";

    public IEnumerable<Detection> Evaluate(
        DetectionContext context)
    {
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