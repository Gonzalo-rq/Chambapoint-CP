using System.Security.Claims;
using ChambaPoint.Api.Hubs;
using ChambaPoint.Api.Models;
using ChambaPoint.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ChambaPoint.Api.Controllers;

[ApiController]
[Route("api/workers/{workerId:int}/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ICacheService _cache;
    private readonly IRequestNotifier _notifier;
    private readonly IHubContext<NotificationsHub> _hub;

    public ReviewsController(IReviewService reviewService, ICacheService cache, IRequestNotifier notifier, IHubContext<NotificationsHub> hub)
    {
        _reviewService = reviewService;
        _cache = cache;
        _notifier = notifier;
        _hub = hub;
    }

    [Authorize(Roles = Roles.Customer)]
    [HttpPost]
    public async Task<IActionResult> Create(int workerId, [FromBody] ReviewInput input, CancellationToken ct)
    {
        if (input.Rating < 1 || input.Rating > 5)
        {
            return BadRequest(new { message = "Rating debe estar entre 1 y 5." });
        }

        if (string.IsNullOrWhiteSpace(input.Text))
        {
            return BadRequest(new { message = "Text es obligatorio." });
        }

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var review = await _reviewService.CreateAsync(workerId, userId.Value, input, ct);
        if (review is null)
        {
            return NotFound(new { message = "Trabajador no encontrado." });
        }

        await _cache.RemoveAsync("worker:" + workerId, ct);

        await _notifier.PublishAsync("review.created", new
        {
            reviewId = review.Id,
            workerId,
            customerId = userId.Value,
            rating = input.Rating
        }, ct);

        var worker = await _reviewService.GetWorkerByWorkerIdAsync(workerId, ct);
        if (worker?.UserId is int workerUserId)
        {
            await _hub.Clients.Group($"user:{workerUserId}").SendAsync("newReview", new
            {
                reviewId = review.Id,
                rating = input.Rating,
                author = User.FindFirstValue("name") ?? User.Identity?.Name
            }, ct);
        }

        return Created($"/api/workers/{workerId}/reviews/{review.Id}", new
        {
            review.Id,
            review.Rating,
            review.Text,
            review.Photos,
            review.CreatedAt,
            author = User.FindFirstValue("name") ?? User.Identity?.Name
        });
    }

    [HttpGet]
    public async Task<IActionResult> List(int workerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 10;

        var (items, total) = await _reviewService.ListByWorkerAsync(workerId, page, pageSize, ct);

        return Ok(new
        {
            items = items.Select(r => new
            {
                r.Id,
                r.Rating,
                r.Text,
                r.Photos,
                r.CreatedAt,
                author = r.Customer != null ? r.Customer.Name : null
            }),
            page,
            pageSize,
            total
        });
    }

    private int? GetUserId()
    {
        var sub = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.TryParse(sub, out var id) ? id : null;
    }
}