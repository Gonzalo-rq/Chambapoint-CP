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
    public async Task<IActionResult> GetRecommended(
        [FromQuery] string? q,
        [FromQuery] string? profession,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        // 1. Obtener candidatos (incluyendo nuevos empleados) filtrados por criterios si existen
        var filteredWorkers = await _workerService.SearchAsync(q, profession, ct);

        // Excluir al propio usuario si es trabajador, salvo que sea el único resultado
        var candidateWorkers = filteredWorkers.Where(w => w.UserId != userId.Value).ToList();
        if (candidateWorkers.Count == 0 && filteredWorkers.Count > 0)
        {
            candidateWorkers = filteredWorkers;
        }

        if (candidateWorkers.Count == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var candidateIds = candidateWorkers.Select(w => w.Id).ToList();

        // 2. Inferencia del modelo ML.NET
        var mlRecommendations = await _recommendationService.RecomendarAsync(
            userId.Value.ToString(),
            candidateIds,
            topK: candidateIds.Count,
            ct);
        var mlScoresByWorkerId = mlRecommendations.ToDictionary(r => r.ProductId, r => r);

        // 3. Historial de categorías solicitadas por el cliente para afinidad
        var customerCategories = await _db.Requests
            .Where(r => r.CustomerId == userId.Value)
            .Select(r => r.Category)
            .Distinct()
            .ToListAsync(ct);

        var preferredCategories = new HashSet<string>(
            customerCategories.Select(WorkerService.NormalizeText),
            StringComparer.OrdinalIgnoreCase);

        // 4. Estadísticas en vivo de reseñas y solicitudes completadas por trabajador
        var reviewStatsList = await _db.Reviews
            .Where(r => candidateIds.Contains(r.WorkerId))
            .GroupBy(r => r.WorkerId)
            .Select(g => new
            {
                WorkerId = g.Key,
                AvgRating = g.Average(r => (double)r.Rating),
                Count = g.Count(),
                PositiveCount = g.Count(r => r.Sentiment == "Positivo")
            })
            .ToListAsync(ct);
        var reviewStatsByWorker = reviewStatsList.ToDictionary(x => x.WorkerId, x => x);

        var completedCounts = await _db.Requests
            .Where(r => r.WorkerId.HasValue && candidateIds.Contains(r.WorkerId.Value) && r.Status == RequestStatuses.Completada)
            .GroupBy(r => r.WorkerId!.Value)
            .Select(g => new { WorkerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WorkerId, x => x.Count, ct);

        // 5. Calcular score dinámico y ordenar según estadísticas reales (reseñas > sin reseñas, rating, trabajos, experiencia)
        var scoredList = candidateWorkers
            .Select(worker =>
            {
                var hasMl = mlScoresByWorkerId.TryGetValue(worker.Id, out var mlRec);
                double? mlScore = (hasMl && !mlRec!.DeFrio) ? mlRec.Score : null;

                int ratingCount = 0;
                double ratingAvg = 0.0;
                int posCount = 0;
                if (reviewStatsByWorker.TryGetValue(worker.Id, out var stats))
                {
                    ratingCount = stats.Count;
                    ratingAvg = stats.AvgRating;
                    posCount = stats.PositiveCount;
                }

                int jobsCompleted = completedCounts.GetValueOrDefault(worker.Id, 0);

                var workerNormCategory = WorkerService.NormalizeText(
                    WorkerService.ResolveCategoryFromTerm(worker.Profession) ?? worker.Profession);
                bool hasAffinity = preferredCategories.Contains(workerNormCategory);

                double score = WorkerService.ComputeWorkerScore(
                    worker,
                    ratingCount,
                    ratingAvg,
                    posCount,
                    jobsCompleted,
                    mlScore,
                    hasAffinity);

                bool esNuevo = ratingCount == 0 && jobsCompleted == 0;

                return new
                {
                    Worker = worker,
                    Score = Math.Round(score, 2),
                    RatingCount = ratingCount,
                    RatingAvg = ratingAvg,
                    JobsCompleted = jobsCompleted,
                    EsNuevo = esNuevo,
                    EsPopular = ratingCount > 0 || jobsCompleted > 0
                };
            })
            .OrderByDescending(x => x.RatingCount > 0)
            .ThenByDescending(x => x.Score)
            .ThenByDescending(x => x.RatingAvg)
            .ThenByDescending(x => x.RatingCount)
            .ThenByDescending(x => x.JobsCompleted)
            .ThenByDescending(x => x.Worker.ExperienceYears)
            .ThenByDescending(x => x.Worker.IsOnline)
            .ThenByDescending(x => x.Worker.Id)
            .Take(20)
            .ToList();

        var dtos = new List<object>();
        foreach (var item in scoredList)
        {
            var dto = await BuildWorkerDtoAsync(item.Worker, includeReviews: false, ct);
            dtos.Add(new
            {
                worker = dto,
                score = item.Score,
                esPopular = item.EsPopular,
                esNuevo = item.EsNuevo
            });
        }

        return Ok(dtos);
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
            userId = worker.UserId,
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
