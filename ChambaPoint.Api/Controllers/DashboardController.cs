using System.Security.Claims;
using ChambaPoint.Api.Models;
using ChambaPoint.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChambaPoint.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Worker)]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("api/dashboard/worker")]
    [HttpGet("api/workers/dashboard")]
    public async Task<IActionResult> GetWorkerDashboard(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var (dashboard, statusCode, error) = await _dashboardService.GetWorkerDashboardAsync(userId.Value, ct);

        if (statusCode == 403) return StatusCode(403, new { message = error });
        if (dashboard == null) return StatusCode(500, new { message = "Error inesperado cargando el dashboard." });

        return Ok(dashboard);
    }

    private int? GetUserId()
    {
        var sub = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.TryParse(sub, out var id) ? id : null;
    }
}
