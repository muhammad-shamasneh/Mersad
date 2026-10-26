using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mersad.Api.Data;
using Mersad.Api.Models;

namespace Mersad.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProcessEventsController : ControllerBase
{
    private readonly MersadDbContext _db;

    public ProcessEventsController(MersadDbContext db)
    {
        _db = db;
    }

    [Authorize(Policy = "Dashboard")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProcessEvent>>> GetEvents()
    {
        return await _db.ProcessEvents
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync();
    }

    [Authorize(Policy = "Agent")]
    [HttpPost]
    public async Task<ActionResult<ProcessEvent>> CreateEvent(ProcessEvent processEvent)
    {
        var deviceExists = await _db.Devices
            .AnyAsync(d => d.Id == processEvent.DeviceId);

        if (!deviceExists)
        {
            return BadRequest("DeviceId does not exist.");
        }

        processEvent.Id = Guid.NewGuid();
        processEvent.Timestamp = DateTime.UtcNow;

        _db.ProcessEvents.Add(processEvent);
        await _db.SaveChangesAsync();

        return Ok(processEvent);
    }
}