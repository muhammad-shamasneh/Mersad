
using Microsoft.EntityFrameworkCore;
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
        var processedKeys =
            new HashSet<(string RuleId, Guid? ProcessEventId,
                Guid? NetworkEventId)>();

        foreach (var detection in detections)
        {
            var key = (
                detection.RuleId,
                detection.ProcessEventId,
                detection.NetworkEventId);

            // Avoid duplicates within this evaluation batch.
            if (!processedKeys.Add(key))
            {
                continue;
            }

            // Avoid recreating an alert for the same rule and event(s).
            var alreadyExists = await _db.Alerts
                .AsNoTracking()
                .AnyAsync(alert =>
                    alert.DetectionRuleId == detection.RuleId &&
                    alert.ProcessEventId == detection.ProcessEventId &&
                    alert.NetworkEventId == detection.NetworkEventId);

            if (alreadyExists)
            {
                continue;
            }

            var now = DateTimeOffset.UtcNow;

            _db.Alerts.Add(new Alert
            {
                Id = Guid.NewGuid(),
                DeviceId = detection.DeviceId,
                DetectionRuleId = detection.RuleId,
                Title = detection.Title,
                Description = detection.Description,
                Severity = detection.Severity,
                Status = "New",
                CreatedAt = now,
                UpdatedAt = now,
                ProcessEventId = detection.ProcessEventId,
                NetworkEventId = detection.NetworkEventId
            });
        }

        if (_db.ChangeTracker.HasChanges())
        {
            await _db.SaveChangesAsync();
        }
    }
}
