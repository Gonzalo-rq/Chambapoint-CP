using System.Security.Claims;
using ChambaPoint.Api.Data;
using ChambaPoint.Api.Models;
using ChambaPoint.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChambaPoint.Api.Controllers;

[ApiController]
[Route("api/workers")]
public class WorkersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWorkerService _workerService;
    private readonly ICacheService _cache;
    private readonly IRecommendationService _recommendationService;

    public WorkersController(
        AppDbContext db,
        IWorkerService workerService,
        ICacheService cache,
        IRecommendationService recommendationService)
    {
        _db = db;
        _workerService = workerService;
        _cache = cache;
        _recommendationService = recommendationService;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] string? profession, CancellationToken ct)
    {
        var workers = await _workerService.SearchAsync(q, profession, ct);
        var dtos = new List<object>();

        foreach (var worker in workers)
        {
            dtos.Add(await BuildWorkerDtoAsync(worker, includeReviews: false, ct));
        }

        return Ok(dtos);
    }

    [HttpGet("recommended")]
    [Authorize]
    public async Task<IActionResult> GetRecommended(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        // 1. Obtener todos los trabajadores activos de la BD con sus usuarios
        var allWorkers = await _db.Workers
            .Include(w => w.User)
            .ToListAsync(ct);

        if (allWorkers.Count == 0)
        {
            return Ok(new List<object>());
        }

        // 2. Ejecutar inferencia del modelo ML
        var allWorkerIds = allWorkers.Select(w => w.Id).ToList();
        var prediccionesML = await _recommendationService.RecomendarAsync(userId.Value.ToString(), allWorkerIds, topK: 10, ct);

        var dtos = new List<object>();

        foreach (var worker in allWorkers)
        {
            var pred = prediccionesML.FirstOrDefault(p => p.ProductId == worker.Id);
            double finalScore;
            bool esNuevo = false;

            if (pred != null && !pred.DeFrio)
            {
                // El modelo ML tiene historial personalizado para este usuario
                finalScore = Math.Round(pred.Score, 2);
            }
            else
            {
                // Trabajador nuevo o usuario sin historial:
                // Si el trabajador tiene calificación promedio, usamos eso; si es nuevo (sin reviews), le asignamos un boost de bienvenida de 4.8
                var ratingStats = await _db.Reviews
                    .Where(r => r.WorkerId == worker.Id)
                    .GroupBy(r => 1)
                    .Select(g => new { Avg = (double?)g.Average(r => r.Rating) })
                    .FirstOrDefaultAsync(ct);

                if (ratingStats?.Avg != null)
                {
                    finalScore = Math.Round(ratingStats.Avg.Value, 1);
                }
                else
                {
                    finalScore = 4.8; // Puntaje inicial para darle visibilidad en el catálogo
                    esNuevo = true;
                }
            }

            dtos.Add(new
            {
                worker = await BuildWorkerDtoAsync(worker, includeReviews: false, ct),
                score = finalScore,
                esPopular = !esNuevo,
                esNuevo = esNuevo
            });
        }

        // Ordenar de mayor a menor score y retornar el top 6
        var resultadoFinal = dtos
            .OrderByDescending(d => ((dynamic)d).score)
            .Take(6)
            .ToList();

        return Ok(resultadoFinal);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var cacheKey = $"worker:{id}";
        var cached = await _cache.GetAsync<object>(cacheKey, ct);
        if (cached is not null)
        {
            return Ok(cached);
        }

        var worker = await _workerService.GetByIdAsync(id, ct);
        if (worker is null)
        {
            return NotFound(new { message = "Trabajador no encontrado." });
        }

        var dto = await BuildWorkerDtoAsync(worker, includeReviews: true, ct);
        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10), ct);

        return Ok(dto);
    }

    [Authorize(Roles = Roles.Worker)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] WorkerProfileInput input, CancellationToken ct)
    {
        var error = ValidateInput(input);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        if (await _db.Workers.AnyAsync(w => w.UserId == userId, ct))
        {
            return Conflict(new { message = "Ya tienes un perfil de trabajador. Usalo con PUT." });
        }

        var worker = await _workerService.CreateProfileAsync(userId.Value, input, ct);
        await _cache.RemoveAsync("user:" + userId, ct);

        var freshWorker = await _workerService.GetByIdAsync(worker.Id, ct);
        return Created($"/api/workers/{worker.Id}", await BuildWorkerDtoAsync(freshWorker!, includeReviews: false, ct));
    }

    [Authorize(Roles = Roles.Worker)]
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] WorkerProfileInput input, CancellationToken ct)
    {
        var error = ValidateInput(input);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var worker = await _workerService.GetByUserIdAsync(userId.Value, ct);
        if (worker is null)
        {
            return NotFound(new { message = "No tienes un perfil de trabajador. Créalo con POST." });
        }

        worker = await _workerService.UpdateProfileAsync(worker, input, ct);
        await _cache.RemoveAsync("worker:" + worker.Id, ct);
        await _cache.RemoveAsync("user:" + userId, ct);

        return Ok(await BuildWorkerDtoAsync(worker, includeReviews: false, ct));
    }

    [Authorize(Roles = Roles.Worker)]
    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var worker = await _workerService.GetByUserIdAsync(userId.Value, ct);
        if (worker is null)
        {
            return NotFound(new { message = "No tienes un perfil de trabajador." });
        }

        _db.Workers.Remove(worker);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync("worker:" + worker.Id, ct);
        await _cache.RemoveAsync("user:" + userId, ct);

        return Ok(new { message = "Perfil de trabajador eliminado." });
    }

    private async Task<object> BuildWorkerDtoAsync(Worker worker, bool includeReviews, CancellationToken ct)
    {
        var ratingStats = await _db.Reviews
            .Where(r => r.WorkerId == worker.Id)
            .GroupBy(r => 1)
            .Select(g => new { Average = (double?)g.Average(r => r.Rating), Count = g.Count() })
            .FirstOrDefaultAsync(ct);

        var jobsCompleted = await _db.Requests
            .CountAsync(r => r.WorkerId == worker.Id && r.Status == RequestStatuses.Completada, ct);

        var reviews = includeReviews
            ? await _db.Reviews
                .Include(r => r.Customer)
                .Where(r => r.WorkerId == worker.Id)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewDto(
                    r.Id,
                    r.Rating,
                    r.Text,
                    r.Photos,
                    r.Sentiment,
                    r.SentimentProbability,
                    r.CreatedAt,
                    r.Customer != null ? r.Customer.Name : null))
                .ToListAsync(ct)
            : new List<ReviewDto>();

        return new
        {
            worker.Id,
            name = worker.User.Name,
            email = worker.User.Email,
            avatarUrl = worker.User.AvatarUrl,
            isOnline = worker.IsOnline,
            worker.Profession,
            worker.ExperienceYears,
            worker.DistanceKm,
            jobsCount = jobsCompleted,
            worker.Latitude,
            worker.Longitude,
            worker.About,
            worker.Certifications,
            worker.Gallery,
            ratingAverage = ratingStats?.Average == null ? 0 : Math.Round(ratingStats.Average.Value, 1),
            ratingCount = ratingStats?.Count ?? 0,
            reviews
        };
    }

    public record ReviewDto(
        int Id,
        int Rating,
        string Text,
        List<string> Photos,
        string? Sentiment,
        double? SentimentProbability,
        DateTime CreatedAt,
        string? Author);

    private int? GetUserId()
    {
        var sub = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.TryParse(sub, out var id) ? id : null;
    }

    private static string? ValidateInput(WorkerProfileInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Profession))
        {
            return "Profession es obligatorio.";
        }

        if (input.ExperienceYears < 0 || input.ExperienceYears > 60)
        {
            return "ExperienceYears debe estar entre 0 y 60.";
        }

        if (input.DistanceKm < 0)
        {
            return "DistanceKm no puede ser negativo.";
        }

        return null;
    }
}
