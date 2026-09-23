using System.Security.Claims;
using ChambaPoint.Api.Models;
using ChambaPoint.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChambaPoint.Api.Controllers;

[ApiController]
[Route("api/workers/{workerId:int}/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ICacheService _cache;

    public ReviewsController(IReviewService reviewService, ICacheService cache)
    {
        _reviewService = reviewService;
        _cache = cache;
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