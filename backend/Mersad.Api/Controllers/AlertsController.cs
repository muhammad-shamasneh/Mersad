using Mersad.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mersad.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public class AlertsController : ControllerBase
{
    private readonly MersadDbContext _db;

    public AlertsController(MersadDbContext db)
    {
        _db = db;
    }

    [Authorize(Policy = "Dashboard")]
    [HttpGet]
    public async Task<IActionResult> GetAlerts()
    {
        var alerts = await _db.Alerts
            .AsNoTracking()
            .OrderByDescending(alert => alert.CreatedAt)
            .ToListAsync();

        return Ok(alerts);
    }
}