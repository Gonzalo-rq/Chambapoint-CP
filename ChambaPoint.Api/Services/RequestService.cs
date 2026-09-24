using ChambaPoint.Api.Data;
using ChambaPoint.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChambaPoint.Api.Services;

public record CreateRequestInput(
    int? WorkerId,
    string Category,
    string Description,
    List<string>? Photos,
    string Urgency,
    DateTime? ScheduledAt
);

public record UpdateRequestStatusInput(
    string Status
);

public interface IRequestService
{
    Task<(Request? Request, string? Error)> CreateAsync(int customerId, CreateRequestInput input, CancellationToken ct = default);
    Task<(List<Request> Items, int Total)> ListAsync(int userId, string role, string? statusFilter, int page, int pageSize, CancellationToken ct = default);
    Task<Request?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<(Request? Request, int StatusCode, string? Error)> UpdateStatusAsync(int requestId, int workerUserId, string newStatus, CancellationToken ct = default);
    Task<Worker?> GetWorkerByUserIdAsync(int userId, CancellationToken ct = default);
}

public class RequestService : IRequestService
{
    private readonly AppDbContext _db;

    public RequestService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(Request? Request, string? Error)> CreateAsync(int customerId, CreateRequestInput input, CancellationToken ct = default)
    {
        if (!RequestCategories.IsValid(input.Category))
        {
            return (null, $"Categoría inválida. Opciones permitidas: {string.Join(", ", RequestCategories.All)}");
        }

        if (!RequestUrgencies.IsValid(input.Urgency))
        {
            return (null, $"Urgencia inválida. Opciones permitidas: {string.Join(", ", RequestUrgencies.All)}");
        }

        if (string.IsNullOrWhiteSpace(input.Description))
        {
            return (null, "La descripción es requerida.");
        }

        if (input.WorkerId.HasValue)
        {
            var workerExists = await _db.Workers.AnyAsync(w => w.Id == input.WorkerId.Value, ct);
            if (!workerExists)
            {
                return (null, "El trabajador especificado no existe.");
            }
        }

        var request = new Request
        {
            CustomerId = customerId,
            WorkerId = input.WorkerId,
            Category = RequestCategories.Normalize(input.Category),
            Description = input.Description.Trim(),
            Photos = input.Photos ?? new List<string>(),
            Urgency = RequestUrgencies.Normalize(input.Urgency),
            ScheduledAt = input.ScheduledAt,
            Status = RequestStatuses.Pendiente,
            CreatedAt = DateTime.UtcNow
        };

        _db.Requests.Add(request);
        await _db.SaveChangesAsync(ct);

        // Cargar relaciones para la respuesta completa
        await _db.Entry(request).Reference(r => r.Customer).LoadAsync(ct);
        if (request.WorkerId.HasValue)
        {
            await _db.Entry(request).Reference(r => r.Worker).Query().Include(w => w.User).LoadAsync(ct);
        }

        return (request, null);
    }

    public async Task<(List<Request> Items, int Total)> ListAsync(int userId, string role, string? statusFilter, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Requests
            .Include(r => r.Customer)
            .Include(r => r.Worker!)
                .ThenInclude(w => w.User)
            .AsQueryable();

        if (string.Equals(role, Roles.Worker, StringComparison.OrdinalIgnoreCase))
        {
            var worker = await _db.Workers.FirstOrDefaultAsync(w => w.UserId == userId, ct);
            if (worker == null)
            {
                return (new List<Request>(), 0);
            }

            // Para trabajadores: solicitudes asignadas directamente o abiertas de su oficio
            query = query.Where(r => r.WorkerId == worker.Id || (r.WorkerId == null && r.Category == worker.Profession));
        }
        else
        {
            // Para clientes: solo sus propias solicitudes
            query = query.Where(r => r.CustomerId == userId);
        }

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            var filter = statusFilter.Trim().ToLowerInvariant();
            if (filter == "activas")
            {
                query = query.Where(r => r.Status == RequestStatuses.Pendiente || r.Status == RequestStatuses.Aceptada);
            }
            else if (filter == "historial")
            {
                query = query.Where(r => r.Status == RequestStatuses.Completada || r.Status == RequestStatuses.Rechazada);
            }
            else if (RequestStatuses.IsValid(statusFilter))
            {
                var normalized = RequestStatuses.Normalize(statusFilter);
                query = query.Where(r => r.Status == normalized);
            }
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<Request?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Requests
            .Include(r => r.Customer)
            .Include(r => r.Worker!)
                .ThenInclude(w => w.User)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<(Request? Request, int StatusCode, string? Error)> UpdateStatusAsync(int requestId, int workerUserId, string newStatus, CancellationToken ct = default)
    {
        if (!RequestStatuses.IsValid(newStatus))
        {
            return (null, 400, $"Estado inválido. Opciones permitidas: {string.Join(", ", RequestStatuses.All)}");
        }

        var normalizedStatus = RequestStatuses.Normalize(newStatus);
        if (normalizedStatus == RequestStatuses.Pendiente)
        {
            return (null, 400, "No se puede cambiar el estado de una solicitud a Pendiente.");
        }

        var request = await _db.Requests
            .Include(r => r.Customer)
            .Include(r => r.Worker!)
                .ThenInclude(w => w.User)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (request == null)
        {
            return (null, 404, "Solicitud no encontrada.");
        }

        var worker = await _db.Workers
            .Include(w => w.User)
            .FirstOrDefaultAsync(w => w.UserId == workerUserId, ct);

        if (worker == null)
        {
            return (null, 403, "El usuario no cuenta con un perfil de trabajador.");
        }

        // Si la solicitud ya tiene un trabajador asignado, solo ese trabajador dueño puede cambiar el estado
        if (request.WorkerId.HasValue && request.WorkerId.Value != worker.Id)
        {
            return (null, 403, "Solo el trabajador asignado a esta solicitud puede modificar su estado.");
        }

        // Si la solicitud era libre (WorkerId == null) y el trabajador la acepta, se le asigna
        if (!request.WorkerId.HasValue)
        {
            if (normalizedStatus == RequestStatuses.Aceptada)
            {
                request.WorkerId = worker.Id;
                request.Worker = worker;
            }
            else
            {
                return (null, 400, "Debe aceptar la solicitud primero antes de rechazarla o completarla.");
            }
        }

        request.Status = normalizedStatus;
        if (normalizedStatus == RequestStatuses.Completada)
        {
            request.CompletedAt = DateTime.UtcNow;
            if (!request.Price.HasValue || request.Price.Value <= 0)
            {
                request.Price = request.Category switch
                {
                    RequestCategories.Plomeria => 150m,
                    RequestCategories.Electricidad => 180m,
                    RequestCategories.Carpinteria => 200m,
                    RequestCategories.Pintura => 220m,
                    _ => 150m
                };
            }
        }
        await _db.SaveChangesAsync(ct);

        return (request, 200, null);
    }

    public Task<Worker?> GetWorkerByUserIdAsync(int userId, CancellationToken ct = default)
    {
        return _db.Workers
            .Include(w => w.User)
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);
    }
}
