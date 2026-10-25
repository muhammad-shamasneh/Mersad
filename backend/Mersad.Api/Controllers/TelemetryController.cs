using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mersad.Api.Data;
using Mersad.Api.Models;
using Mersad.Api.Detection;
using Mersad.Api.Alerts;

namespace Mersad.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelemetryController : ControllerBase
{
    private readonly MersadDbContext _db;
    private readonly DetectionEngine _detectionEngine;
    private readonly AlertService _alertService;

    public TelemetryController(
        MersadDbContext db,
        DetectionEngine detectionEngine,
        AlertService alertService)
    {
        _db = db;
        _detectionEngine = detectionEngine;
        _alertService = alertService;
    }

    [HttpPost]
    public async Task<IActionResult> Ingest(
        TelemetryRequest request)
    {
        if (request.AgentId == Guid.Empty)
        {
            return BadRequest("AgentId is required.");
        }

        if (request.Events is null || request.Events.Count == 0)
        {
            return BadRequest("At least one telemetry event is required.");
        }

        var device = await _db.Devices
            .FirstOrDefaultAsync(d => d.AgentId == request.AgentId);

        if (device is null)
        {
            return NotFound("AgentId is not registered.");
        }

        var now = DateTimeOffset.UtcNow;

        var processEvents = new List<ProcessEvent>();
        var networkEvents = new List<NetworkEvent>();

        foreach (var telemetryEvent in request.Events)
        {
            if (telemetryEvent.Timestamp > now.AddMinutes(5))
            {
                return BadRequest(
                    "Telemetry timestamp is too far in the future.");
            }

            if (telemetryEvent.Timestamp < now.AddHours(-24))
            {
                return BadRequest(
                    "Telemetry timestamp is too old.");
            }

            if (string.IsNullOrWhiteSpace(telemetryEvent.Type))
            {
                return BadRequest(
                    "Telemetry event type is required.");
            }

            if (telemetryEvent.Type.Equals(
                    "process",
                    StringComparison.OrdinalIgnoreCase))
            {
                var data = telemetryEvent.Data.Deserialize<ProcessTelemetryData>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (data is null ||
                    data.ProcessId <= 0 ||
                    string.IsNullOrWhiteSpace(data.ProcessName))
                {
                    return BadRequest(
                        "Invalid process telemetry.");
                }

                var processEvent = new ProcessEvent
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.Id,
                    ProcessId = data.ProcessId,
                    ProcessName = data.ProcessName,
                    Timestamp = telemetryEvent.Timestamp
                };

                _db.ProcessEvents.Add(processEvent);
                processEvents.Add(processEvent);
            }
            else if (telemetryEvent.Type.Equals(
                         "network",
                         StringComparison.OrdinalIgnoreCase))
            {
                var data = telemetryEvent.Data.Deserialize<NetworkTelemetryData>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (data is null ||
                    data.ProcessId <= 0 ||
                    string.IsNullOrWhiteSpace(data.LocalAddress) ||
                    data.LocalPort < 0 ||
                    data.LocalPort > 65535 ||
                    string.IsNullOrWhiteSpace(data.RemoteAddress) ||
                    data.RemotePort < 0 ||
                    data.RemotePort > 65535 ||
                    string.IsNullOrWhiteSpace(data.State))
                {
                    return BadRequest(
                        "Invalid network telemetry.");
                }

                var networkEvent = new NetworkEvent
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.Id,
                    ProcessId = data.ProcessId,
                    LocalAddress = data.LocalAddress,
                    LocalPort = data.LocalPort,
                    RemoteAddress = data.RemoteAddress,
                    RemotePort = data.RemotePort,
                    State = data.State,
                    Timestamp = telemetryEvent.Timestamp
                };

                _db.NetworkEvents.Add(networkEvent);
                networkEvents.Add(networkEvent);
            }
            else
            {
                return BadRequest(
                    $"Unsupported telemetry type: {telemetryEvent.Type}");
            }
        }

        device.LastSeen = DateTime.UtcNow;

        await _db.SaveChangesAsync();


        var correlationStart = now.AddMinutes(-5);
        var correlationEnd = now.AddMinutes(5);

        // Load events for this device so process and network telemetry
        // from separate HTTP requests can be correlated.
        var recentProcessEvents = (await _db.ProcessEvents
            .AsNoTracking()
            .Where(e => e.DeviceId == device.Id)
            .ToListAsync())
            .Where(e =>
                e.Timestamp >= correlationStart &&
                e.Timestamp <= correlationEnd)
            .ToList();

        var recentNetworkEvents = (await _db.NetworkEvents
            .AsNoTracking()
            .Where(e => e.DeviceId == device.Id)
            .ToListAsync())
            .Where(e =>
                e.Timestamp >= correlationStart &&
                e.Timestamp <= correlationEnd)
            .ToList();

        var detectionContext = new DetectionContext(
            recentProcessEvents,
            recentNetworkEvents);


        var detections = _detectionEngine
            .Evaluate(detectionContext)
            .ToList();

        foreach (var detection in detections)
        {
            Console.WriteLine(
                $"Detection: {detection.RuleId} - {detection.Title}");
        }

        await _alertService.CreateAlertsAsync(detections);

        return Ok(new
        {
            accepted = request.Events.Count
        });
    }
}
