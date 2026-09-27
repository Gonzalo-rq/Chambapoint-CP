using ChambaPoint.Api.Data;
using ChambaPoint.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChambaPoint.Api.Services;

public interface IReviewService
{
    Task<(Review? Review, int StatusCode, string? Error)> CreateAsync(
        int workerId, int customerId, ReviewInput input, CancellationToken ct = default);
    Task<(List<Review> Items, int Total)> ListByWorkerAsync(
        int workerId, int page, int pageSize, CancellationToken ct = default);
    Task<Worker?> GetWorkerByWorkerIdAsync(int workerId, CancellationToken ct = default);
}

public record ReviewInput(int Rating, string Text, List<string>? Photos);

public class ReviewService : IReviewService
{
    private readonly AppDbContext _db;

    public ReviewService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(Review? Review, int StatusCode, string? Error)> CreateAsync(
        int workerId, int customerId, ReviewInput input, CancellationToken ct = default)
    {
        var workerExists = await _db.Workers.AnyAsync(w => w.Id == workerId, ct);
        if (!workerExists)
        {
            return (null, 404, "Trabajador no encontrado.");
        }

        var hasCompletedRequest = await _db.Requests.AnyAsync(r =>
            r.CustomerId == customerId &&
            r.WorkerId == workerId &&
            r.Status == RequestStatuses.Completada, ct);

        if (!hasCompletedRequest)
        {
            return (null, 403, "Solo clientes con solicitudes completadas con este trabajador pueden dejar reseña.");
        }

        var reviewExists = await _db.Reviews.AnyAsync(r =>
            r.WorkerId == workerId &&
            r.CustomerId == customerId, ct);

        if (reviewExists)
        {
            return (null, 409, "Ya has dejado una reseña para este trabajador.");
        }

        var review = new Review
        {
            WorkerId = workerId,
            CustomerId = customerId,
            Rating = input.Rating,
            Text = input.Text ?? string.Empty,
            Photos = input.Photos ?? new List<string>()
        };

        _db.Reviews.Add(review);

        try
        {
            await _db.SaveChangesAsync(ct);
            return (review, 201, null);
        }
        catch (DbUpdateException)
        {
            return (null, 409, "Ya has dejado una reseña para este trabajador.");
        }
    }

    public async Task<(List<Review> Items, int Total)> ListByWorkerAsync(int workerId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Reviews
            .Include(r => r.Customer)
            .Where(r => r.WorkerId == workerId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<Worker?> GetWorkerByWorkerIdAsync(int workerId, CancellationToken ct = default)
    {
        return _db.Workers.FirstOrDefaultAsync(w => w.Id == workerId, ct);
    }
}