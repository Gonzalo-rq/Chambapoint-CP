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
    int JobsCount,
    string About,
    List<string>? Certifications,
    List<string>? Gallery);

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
        var worker = new Worker
        {
            UserId = userId,
            Profession = input.Profession,
            ExperienceYears = input.ExperienceYears,
            DistanceKm = input.DistanceKm,
            JobsCount = input.JobsCount,
            About = input.About ?? string.Empty,
            Certifications = input.Certifications ?? new List<string>(),
            Gallery = input.Gallery ?? new List<string>()
        };

        _db.Workers.Add(worker);
        await _db.SaveChangesAsync(ct);
        return worker;
    }

    public async Task<Worker> UpdateProfileAsync(Worker worker, WorkerProfileInput input, CancellationToken ct = default)
    {
        worker.Profession = input.Profession;
        worker.ExperienceYears = input.ExperienceYears;
        worker.DistanceKm = input.DistanceKm;
        worker.JobsCount = input.JobsCount;
        worker.About = input.About ?? string.Empty;
        worker.Certifications = input.Certifications ?? worker.Certifications;
        worker.Gallery = input.Gallery ?? worker.Gallery;

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