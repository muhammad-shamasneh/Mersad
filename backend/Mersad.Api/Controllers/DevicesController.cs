using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Mersad.Api.Data;
using Mersad.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Mersad.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly MersadDbContext _db;

    public DevicesController(MersadDbContext db)
    {
        _db = db;
    }

    [Authorize(Policy = "Dashboard")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Device>>> GetDevices()
    {
        return await _db.Devices.OrderByDescending(d => d.LastSeen).ToListAsync();
    }

    [Authorize(Policy = "Agent")]
    [HttpPost]
    public async Task<ActionResult<Device>> RegisterDevice(Device device)
    {
        var existingDevice = await _db.Devices
            .FirstOrDefaultAsync(d => d.AgentId == device.AgentId);

        if (existingDevice is not null)
        {
            existingDevice.Hostname = device.Hostname;
            existingDevice.OS = device.OS;
            existingDevice.OSVersion = device.OSVersion;
            existingDevice.LastSeen = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(existingDevice);
        }

        device.Id = Guid.NewGuid();
        device.LastSeen = DateTime.UtcNow;

        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetDevice),
            new { id = device.Id },
            device);
    }

    [Authorize(Policy = "Dashboard")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Device>> GetDevice(Guid id)
    {
        var device = await _db.Devices
            .Include(d => d.ProcessEvents)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (device is null)
            return NotFound();

        return device;
    }
}
