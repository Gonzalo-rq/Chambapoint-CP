using System.Security.Claims;
using ChambaPoint.Api.Hubs;
using ChambaPoint.Api.Models;
using ChambaPoint.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ChambaPoint.Api.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;
    private readonly IRequestNotifier _notifier;
    private readonly IHubContext<NotificationsHub> _hub;

    public AppointmentsController(
        IAppointmentService appointmentService,
        IRequestNotifier notifier,
        IHubContext<NotificationsHub> hub)
    {
        _appointmentService = appointmentService;
        _notifier = notifier;
        _hub = hub;
    }

    [HttpPost]
    public async Task<IActionResult> Schedule([FromBody] ScheduleAppointmentInput input, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var role = GetUserRole() ?? Roles.Customer;

        var (appointment, statusCode, error, affectedUserId) = await _appointmentService.ScheduleAsync(userId.Value, role, input, ct);

        if (statusCode == 400) return BadRequest(new { message = error });
        if (statusCode == 403) return StatusCode(403, new { message = error });
        if (statusCode == 404) return NotFound(new { message = error });
        if (appointment == null) return StatusCode(500, new { message = "Error interno agendando la cita." });

        var creatorName = User.FindFirstValue("name") ?? User.Identity?.Name;

        // Notificar al usuario afectado vía RabbitMQ
        await _notifier.PublishAsync("appointment.scheduled", new
        {
            appointmentId = appointment.Id,
            requestId = appointment.RequestId,
            workerId = appointment.WorkerId,
            customerId = appointment.CustomerId,
            dateTime = appointment.DateTime,
            description = appointment.Description,
            status = appointment.Status
        }, ct);

        // Notificar en tiempo real vía SignalR
        if (affectedUserId.HasValue)
        {
            await _hub.Clients.Group($"user:{affectedUserId.Value}").SendAsync("newAppointment", new
            {
                appointmentId = appointment.Id,
                requestId = appointment.RequestId,
                creatorName,
                dateTime = appointment.DateTime,
                description = appointment.Description,
                status = appointment.Status
            }, ct);
        }

        return Created($"/api/appointments/{appointment.Id}", MapToDto(appointment));
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status = null,
        [FromQuery] int? requestId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        var (items, total) = await _appointmentService.ListAsync(userId.Value, status, requestId, page, pageSize, ct);

        return Ok(new
        {
            items = items.Select(MapToDto),
            page,
            pageSize,
            total
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var appointment = await _appointmentService.GetByIdAsync(id, ct);
        if (appointment == null)
        {
            return NotFound(new { message = "Cita no encontrada." });
        }

        var isCustomer = appointment.CustomerId == userId.Value;
        var isWorker = appointment.Worker?.UserId == userId.Value;

        if (!isCustomer && !isWorker && GetUserRole() != "Admin")
        {
            return Forbid();
        }

        return Ok(MapToDto(appointment));
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateAppointmentStatusInput input, CancellationToken ct)
    {
        return await HandleStatusUpdate(id, input, ct);
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatusDirect(int id, [FromBody] UpdateAppointmentStatusInput input, CancellationToken ct)
    {
        return await HandleStatusUpdate(id, input, ct);
    }

    private async Task<IActionResult> HandleStatusUpdate(int id, UpdateAppointmentStatusInput input, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(input.Status))
        {
            return BadRequest(new { message = "El nuevo estado es requerido." });
        }

        var (appointment, statusCode, error, affectedUserId) = await _appointmentService.UpdateStatusAsync(id, userId.Value, input.Status, ct);

        if (statusCode == 400) return BadRequest(new { message = error });
        if (statusCode == 403) return StatusCode(403, new { message = error });
        if (statusCode == 404) return NotFound(new { message = error });
        if (appointment == null) return StatusCode(500, new { message = "Error inesperado al actualizar la cita." });

        var changerName = User.FindFirstValue("name") ?? User.Identity?.Name;

        // Notificar vía RabbitMQ
        await _notifier.PublishAsync("appointment.status_changed", new
        {
            appointmentId = appointment.Id,
            requestId = appointment.RequestId,
            status = appointment.Status,
            updatedAt = DateTime.UtcNow
        }, ct);

        // Notificar en tiempo real vía SignalR
        if (affectedUserId.HasValue)
        {
            await _hub.Clients.Group($"user:{affectedUserId.Value}").SendAsync("appointmentStatusChanged", new
            {
                appointmentId = appointment.Id,
                status = appointment.Status,
                changedBy = changerName,
                dateTime = appointment.DateTime
            }, ct);
        }

        return Ok(MapToDto(appointment));
    }

    private static object MapToDto(Appointment a) => new
    {
        a.Id,
        a.RequestId,
        request = a.Request != null ? new
        {
            a.Request.Id,
            a.Request.Category,
            a.Request.Description,
            a.Request.Status
        } : null,
        a.WorkerId,
        worker = a.Worker != null ? new
        {
            a.Worker.Id,
            a.Worker.UserId,
            name = a.Worker.User?.Name,
            a.Worker.Profession,
            avatarUrl = a.Worker.User?.AvatarUrl
        } : null,
        a.CustomerId,
        customer = a.Customer != null ? new
        {
            a.Customer.Id,
            a.Customer.Name,
            a.Customer.Email,
            a.Customer.AvatarUrl
        } : null,
        a.DateTime,
        a.Description,
        a.Status,
        a.CreatedAt
    };

    private int? GetUserId()
    {
        var sub = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.TryParse(sub, out var id) ? id : null;
    }

    private string? GetUserRole()
    {
        return User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
    }
}
