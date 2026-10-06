using Mersad.Api.Data;
using Mersad.Api.Models;

namespace Mersad.Api.Alerts;

public class AlertService
{
    private readonly MersadDbContext _db;

    public AlertService(MersadDbContext db)
    {
        _db = db;
    }

    public async Task CreateAlertsAsync(
        IEnumerable<Mersad.Api.Detection.Detection> detections)
    {
        foreach (var detection in detections)
        {
            var alert = new Alert
            {
                Id = Guid.NewGuid(),
                DeviceId = detection.DeviceId,
                DetectionRuleId = detection.RuleId,
                Title = detection.Title,
                Description = detection.Description,
                Severity = detection.Severity,
                Status = "New",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                ProcessEventId = detection.ProcessEventId,
                NetworkEventId = detection.NetworkEventId
            };

            _db.Alerts.Add(alert);
        }

        await _db.SaveChangesAsync();
    }
}