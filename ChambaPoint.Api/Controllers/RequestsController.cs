using System.Security.Claims;
using ChambaPoint.Api.Hubs;
using ChambaPoint.Api.Models;
using ChambaPoint.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RequestModel = ChambaPoint.Api.Models.Request;

namespace ChambaPoint.Api.Controllers;

[ApiController]
[Route("api/requests")]
public class RequestsController : ControllerBase
{
    private readonly IRequestService _requestService;
    private readonly IRequestNotifier _notifier;
    private readonly IHubContext<NotificationsHub> _hub;

    public RequestsController(
        IRequestService requestService,
        IRequestNotifier notifier,
        IHubContext<NotificationsHub> hub)
    {
        _requestService = requestService;
        _notifier = notifier;
        _hub = hub;
    }

    [Authorize(Roles = Roles.Customer)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequestInput input, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var (request, error) = await _requestService.CreateAsync(userId.Value, input, ct);
        if (request == null)
        {
            return BadRequest(new { message = error });
        }

        // Publicar evento en RabbitMQ / Notifier
        await _notifier.PublishAsync("request.created", new
        {
            requestId = request.Id,
            customerId = request.CustomerId,
            customerName = request.Customer?.Name,
            workerId = request.WorkerId,
            category = request.Category,
            description = request.Description,
            urgency = request.Urgency,
            scheduledAt = request.ScheduledAt,
            status = request.Status,
            createdAt = request.CreatedAt
        }, ct);

        // Notificar en tiempo real vía SignalR
        if (request.Worker?.UserId is int workerUserId)
        {
            await _hub.Clients.Group($"user:{workerUserId}").SendAsync("newRequest", new
            {
                requestId = request.Id,
                customerName = request.Customer?.Name,
                category = request.Category,
                urgency = request.Urgency,
                createdAt = request.CreatedAt
            }, ct);
        }

        return Created($"/api/requests/{request.Id}", MapToDto(request));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var role = GetUserRole() ?? Roles.Customer;
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 10;

        var (items, total) = await _requestService.ListAsync(userId.Value, role, status, page, pageSize, ct);

        return Ok(new
        {
            items = items.Select(MapToDto),
            page,
            pageSize,
            total
        });
    }

    [Authorize]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var request = await _requestService.GetByIdAsync(id, ct);
        if (request == null)
        {
            return NotFound(new { message = "Solicitud no encontrada." });
        }

        var role = GetUserRole();
        var isCustomer = request.CustomerId == userId.Value;
        var isWorker = request.Worker?.UserId == userId.Value;

        if (!isCustomer && !isWorker && role != "Admin")
        {
            return Forbid();
        }

        return Ok(MapToDto(request));
    }

    [Authorize(Roles = Roles.Worker)]
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateRequestStatusInput input, CancellationToken ct)
    {
        return await HandleStatusUpdate(id, input, ct);
    }

    [Authorize(Roles = Roles.Worker)]
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatusDirect(int id, [FromBody] UpdateRequestStatusInput input, CancellationToken ct)
    {
        return await HandleStatusUpdate(id, input, ct);
    }

    private async Task<IActionResult> HandleStatusUpdate(int id, UpdateRequestStatusInput input, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(input.Status))
        {
            return BadRequest(new { message = "El nuevo estado es requerido." });
        }

        var (request, statusCode, error) = await _requestService.UpdateStatusAsync(id, userId.Value, input.Status, ct);

        if (statusCode == 404) return NotFound(new { message = error });
        if (statusCode == 400) return BadRequest(new { message = error });
        if (statusCode == 403) return StatusCode(403, new { message = error });
        if (request == null) return StatusCode(500, new { message = "Error inesperado al actualizar la solicitud." });

        // Notificar por RabbitMQ
        await _notifier.PublishAsync("request.status_changed", new
        {
            requestId = request.Id,
            status = request.Status,
            workerId = request.WorkerId,
            customerId = request.CustomerId,
            updatedAt = DateTime.UtcNow
        }, ct);

        // Notificar en tiempo real por SignalR al cliente
        await _hub.Clients.Group($"user:{request.CustomerId}").SendAsync("requestStatusChanged", new
        {
            requestId = request.Id,
            status = request.Status,
            workerName = request.Worker?.User?.Name
        }, ct);

        return Ok(MapToDto(request));
    }

    private static object MapToDto(RequestModel request) => new
    {
        request.Id,
        request.CustomerId,
        customer = request.Customer != null ? new
        {
            request.Customer.Id,
            request.Customer.Name,
            request.Customer.Email,
            request.Customer.AvatarUrl
        } : null,
        request.WorkerId,
        worker = request.Worker != null ? new
        {
            request.Worker.Id,
            request.Worker.UserId,
            name = request.Worker.User?.Name,
            request.Worker.Profession,
            avatarUrl = request.Worker.User?.AvatarUrl
        } : null,
        request.Category,
        request.Description,
        request.Photos,
        request.Urgency,
        request.ScheduledAt,
        request.Status,
        request.CreatedAt
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
