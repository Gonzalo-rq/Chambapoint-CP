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
        // Coordenadas base en Lima Metropolitana (dispersión aleatoria leve alrededor del centro si no se enviaron)
        var random = new Random();
        var defaultLat = -12.046374 + (random.NextDouble() - 0.5) * 0.08;
        var defaultLng = -77.042793 + (random.NextDouble() - 0.5) * 0.08;

        var worker = new Worker
        {
            UserId = userId,
            Profession = input.Profession.Trim(),
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
        worker.Profession = input.Profession.Trim();
        worker.ExperienceYears = input.ExperienceYears;
        worker.DistanceKm = input.DistanceKm;
        worker.About = input.About ?? string.Empty;
        worker.Certifications = input.Certifications ?? worker.Certifications;
        worker.Gallery = input.Gallery ?? worker.Gallery;

        if (input.IsOnline.HasValue)
        {
            worker.IsOnline = input.IsOnline.Value;
        }

        if (input.Latitude.HasValue && input.Longitude.HasValue)
        {
            worker.Latitude = input.Latitude.Value;
            worker.Longitude = input.Longitude.Value;
        }

        await _db.SaveChangesAsync(ct);
        return worker;
    }

    public async Task<List<Worker>> SearchAsync(string? q, string? profession, CancellationToken ct = default)
    {
        var query = _db.Workers
            .Include(w => w.User)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(profession))
        {
            query = query.Where(w => w.Profession.ToLower() == profession.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(w =>
                w.User.Name.ToLower().Contains(term) ||
                w.Profession.ToLower().Contains(term));
        }

        return await query
            .OrderByDescending(w => w.IsOnline)
            .ThenBy(w => w.DistanceKm)
            .Take(50)
            .ToListAsync(ct);
    }
}
