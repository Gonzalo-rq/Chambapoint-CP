using ChambaPoint.Api.Data;
using ChambaPoint.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChambaPoint.Api.Services;

public interface IReviewService
{
    Task<Review?> CreateAsync(int workerId, int customerId, ReviewInput input, CancellationToken ct = default);
    Task<(List<Review> Items, int Total)> ListByWorkerAsync(int workerId, int page, int pageSize, CancellationToken ct = default);
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

    public async Task<Review?> CreateAsync(int workerId, int customerId, ReviewInput input, CancellationToken ct = default)
    {
        var workerExists = await _db.Workers.AnyAsync(w => w.Id == workerId, ct);
        if (!workerExists)
        {
            return null;
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
        await _db.SaveChangesAsync(ct);
        return review;
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