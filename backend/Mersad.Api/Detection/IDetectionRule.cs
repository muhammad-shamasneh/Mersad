using Mersad.Api.Models;

namespace Mersad.Api.Detection;

public interface IDetectionRule
{
    string RuleId { get; }

    IEnumerable<Detection> Evaluate(
        DetectionContext context);
}