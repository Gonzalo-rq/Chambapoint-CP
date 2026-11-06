using System.Globalization;
using System.Text;
using ChambaPoint.Api.Data;
using ChambaPoint.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChambaPoint.Api.Services;

public interface IWorkerService
{
    Task<Worker?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Worker?> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<Worker> CreateProfileAsync(int userId, WorkerProfileInput input, CancellationToken ct = default);
    Task<Worker> UpdateProfileAsync(Worker worker, WorkerProfileInput input, CancellationToken ct = default);
    Task<List<Worker>> SearchAsync(string? q, string? profession, CancellationToken ct = default);
}

public record WorkerProfileInput(
    string Profession,
    int ExperienceYears,
    double DistanceKm,
    string About,
    List<string>? Certifications,
    List<string>? Gallery,
    bool? IsOnline = true,
    double? Latitude = null,
    double? Longitude = null);

public class WorkerService : IWorkerService
{
    private readonly AppDbContext _db;

    public WorkerService(AppDbContext db)
    {
        _db = db;
    }

    public static string NormalizeText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var normalized = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string? ResolveCategoryFromTerm(string? term)
    {
        var t = NormalizeText(term);
        if (string.IsNullOrEmpty(t)) return null;

        if (t.Contains("plomer") || t.Contains("gasfit") || t.Contains("tuber") || t.Contains("agua") || t.Contains("desague"))
            return RequestCategories.Plomeria;
        if (t.Contains("electri") || t.Contains("luz") || t.Contains("cable") || t.Contains("tablero") || t.Contains("enchufe"))
            return RequestCategories.Electricidad;
        if (t.Contains("carpint") || t.Contains("mader") || t.Contains("mueble") || t.Contains("puerta") || t.Contains("closet"))
            return RequestCategories.Carpinteria;
        if (t.Contains("pintur") || t.Contains("pintor") || t.Contains("pintar") || t.Contains("pared"))
            return RequestCategories.Pintura;

        return null;
    }

    public static double ComputeWorkerScore(
        Worker worker,
        int ratingCount,
        double ratingAverage,
        int positiveSentimentCount,
        int jobsCompleted,
        double? mlScore = null,
        bool hasCategoryAffinity = false)
    {
        double experienceBonus = Math.Min(Math.Max(worker.ExperienceYears, 0), 30) * 0.025; // hasta +0.75
        double jobsBonus = Math.Min(Math.Max(jobsCompleted, 0), 20) * 0.08;                // hasta +1.60
        double certBonus = (worker.Certifications != null ? Math.Min(worker.Certifications.Count, 5) : 0) * 0.05; // hasta +0.25
        double onlineBonus = worker.IsOnline ? 0.25 : 0.0;
        double affinityBonus = hasCategoryAffinity ? 0.20 : 0.0;
        double mlAdjustment = mlScore.HasValue ? Math.Clamp((mlScore.Value - 3.0) * 0.1, -0.15, 0.20) : 0.0;

        if (ratingCount > 0)
        {
            // Empleados CON reseñas: rango [3.50 .. 5.00]
            // Siempre se posicionan por encima de los empleados que aún no tienen reseñas (máx 3.45)
            double reviewVolumeBonus = Math.Min(ratingCount, 10) * 0.12;
            double sentimentRatio = (double)positiveSentimentCount / ratingCount;
            double sentimentBonus = sentimentRatio * 0.20;

            double rawWithReviews = 3.50
                + ((ratingAverage - 1.0) / 4.0) * 0.90
                + reviewVolumeBonus
                + sentimentBonus
                + (experienceBonus * 0.35)
                + (jobsBonus * 0.50)
                + certBonus
                + (onlineBonus * 0.40)
                + affinityBonus
                + mlAdjustment;

            return Math.Round(Math.Clamp(rawWithReviews, 3.50, 5.00), 3);
        }
        else
        {
            // Empleados SIN reseñas (incluyendo nuevos empleados): rango [1.50 .. 3.45]
            // Se reordenan dinámicamente según trabajos completados, años de experiencia, certificaciones y estado en línea
            double rawWithoutReviews = 2.00
                + experienceBonus
                + jobsBonus
                + certBonus
                + onlineBonus
                + affinityBonus
                + mlAdjustment;

            return Math.Round(Math.Clamp(rawWithoutReviews, 1.50, 3.45), 3);
        }
    }

    public Task<Worker?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return _db.Workers
            .Include(w => w.User)
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public Task<Worker?> GetByUserIdAsync(int userId, CancellationToken ct = default)
    {
        return _db.Workers
            .Include(w => w.User)
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);
    }

    public async Task<Worker> CreateProfileAsync(int userId, WorkerProfileInput input, CancellationToken ct = default)
    {
        var profession = RequestCategories.IsValid(input.Profession)
            ? RequestCategories.Normalize(input.Profession)
            : (ResolveCategoryFromTerm(input.Profession) ?? input.Profession.Trim());

        var random = new Random();
        var defaultLat = -12.046374 + (random.NextDouble() - 0.5) * 0.06;
        var defaultLng = -77.042793 + (random.NextDouble() - 0.5) * 0.06;

        var worker = new Worker
        {
            UserId = userId,
            Profession = profession,
            ExperienceYears = input.ExperienceYears,
            DistanceKm = input.DistanceKm > 0 ? input.DistanceKm : 10.0,
            About = input.About ?? string.Empty,
            Certifications = input.Certifications ?? new List<string>(),
            Gallery = input.Gallery ?? new List<string>(),
            IsOnline = input.IsOnline ?? true,
            Latitude = input.Latitude ?? defaultLat,
            Longitude = input.Longitude ?? defaultLng,
            CreatedAt = DateTime.UtcNow
        };

        _db.Workers.Add(worker);
        await _db.SaveChangesAsync(ct);
        return worker;
    }

    public async Task<Worker> UpdateProfileAsync(Worker worker, WorkerProfileInput input, CancellationToken ct = default)
    {
        var profession = RequestCategories.IsValid(input.Profession)
            ? RequestCategories.Normalize(input.Profession)
            : (ResolveCategoryFromTerm(input.Profession) ?? input.Profession.Trim());

        worker.Profession = profession;
        worker.ExperienceYears = input.ExperienceYears;
        worker.DistanceKm = input.DistanceKm > 0 ? input.DistanceKm : worker.DistanceKm;
        worker.About = input.About ?? string.Empty;
        worker.Certifications = input.Certifications ?? worker.Certifications;
        worker.Gallery = input.Gallery ?? worker.Gallery;
        worker.IsOnline = input.IsOnline ?? true;

        if (input.Latitude.HasValue && input.Longitude.HasValue)
        {
            worker.Latitude = input.Latitude.Value;
            worker.Longitude = input.Longitude.Value;
        }
        else if (worker.Latitude is null || worker.Longitude is null)
        {
            var random = new Random();
            worker.Latitude = -12.046374 + (random.NextDouble() - 0.5) * 0.06;
            worker.Longitude = -77.042793 + (random.NextDouble() - 0.5) * 0.06;
        }

        await _db.SaveChangesAsync(ct);
        return worker;
    }

    public async Task<List<Worker>> SearchAsync(string? q, string? profession, CancellationToken ct = default)
    {
        var workers = await _db.Workers
            .Include(w => w.User)
            .Include(w => w.Reviews)
            .AsNoTracking()
            .ToListAsync(ct);

        IEnumerable<Worker> filtered = workers;

        if (!string.IsNullOrWhiteSpace(profession))
        {
            var targetCategory = ResolveCategoryFromTerm(profession) ?? profession.Trim();
            var normTarget = NormalizeText(targetCategory);

            filtered = filtered.Where(w =>
            {
                var wCat = ResolveCategoryFromTerm(w.Profession) ?? w.Profession;
                return NormalizeText(wCat) == normTarget || NormalizeText(w.Profession) == normTarget;
            });
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = NormalizeText(q);
            var mappedCategory = ResolveCategoryFromTerm(q);
            var normMappedCategory = mappedCategory != null ? NormalizeText(mappedCategory) : null;

            filtered = filtered.Where(w =>
            {
                var nameMatch = w.User != null && NormalizeText(w.User.Name).Contains(term);
                var profMatch = NormalizeText(w.Profession).Contains(term);
                var aboutMatch = NormalizeText(w.About).Contains(term);
                var certMatch = w.Certifications != null && w.Certifications.Any(c => NormalizeText(c).Contains(term));
                var categorySynonymMatch = normMappedCategory != null &&
                    NormalizeText(ResolveCategoryFromTerm(w.Profession) ?? w.Profession) == normMappedCategory;

                return nameMatch || profMatch || aboutMatch || certMatch || categorySynonymMatch;
            });
        }

        var filteredList = filtered.ToList();
        if (filteredList.Count == 0)
        {
            return filteredList;
        }

        var workerIds = filteredList.Select(w => w.Id).ToList();
        var completedCounts = await _db.Requests
            .Where(r => r.WorkerId.HasValue && workerIds.Contains(r.WorkerId.Value) && r.Status == RequestStatuses.Completada)
            .GroupBy(r => r.WorkerId!.Value)
            .Select(g => new { WorkerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WorkerId, x => x.Count, ct);

        return filteredList
            .Select(w =>
            {
                var reviews = w.Reviews ?? new List<Review>();
                int ratingCount = reviews.Count;
                double ratingAvg = ratingCount > 0 ? reviews.Average(r => (double)r.Rating) : 0.0;
                int posCount = reviews.Count(r => r.Sentiment == "Positivo");
                int jobsCompleted = completedCounts.GetValueOrDefault(w.Id, 0);
                double score = ComputeWorkerScore(w, ratingCount, ratingAvg, posCount, jobsCompleted);

                return new
                {
                    Worker = w,
                    RatingCount = ratingCount,
                    RatingAvg = ratingAvg,
                    JobsCompleted = jobsCompleted,
                    Score = score
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
            .Take(50)
            .Select(x => x.Worker)
            .ToList();
    }
}
