using Mersad.Api.Models;

namespace Mersad.Api.Detection;

public class DetectionEngine
{
    private readonly IEnumerable<IDetectionRule> _rules;

    public DetectionEngine(IEnumerable<IDetectionRule> rules)
    {
        _rules = rules;
    }

    public IEnumerable<Detection> Evaluate(
        DetectionContext context)
    {
        foreach (var rule in _rules)
        {
            foreach (var detection in rule.Evaluate(context))
            {
                yield return detection;
            }
        }
    }
}