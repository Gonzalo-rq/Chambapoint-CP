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
    Task<(Request? Request, int StatusCode, string? Error)> UpdateStatusAsync(
        int requestId, int currentUserId, string role, string newStatus, CancellationToken ct = default);
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

    public async Task<(Request? Request, int StatusCode, string? Error)> UpdateStatusAsync(
        int requestId,
        int currentUserId,
        string role,
        string newStatus,
        CancellationToken ct = default)
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

        if (request.Status == RequestStatuses.Completada || request.Status == RequestStatuses.Rechazada)
        {
            return (null, 400, $"No se puede modificar una solicitud en estado {request.Status}.");
        }

        var isCustomer = request.CustomerId == currentUserId;

        if (isCustomer)
        {
            if (request.Status != RequestStatuses.Pendiente || normalizedStatus != RequestStatuses.Rechazada)
            {
                return (null, 400, "El cliente solo puede cancelar solicitudes en estado Pendiente.");
            }

            request.Status = RequestStatuses.Rechazada;
            await _db.SaveChangesAsync(ct);
            return (request, 200, null);
        }

        var worker = await _db.Workers
            .Include(w => w.User)
            .FirstOrDefaultAsync(w => w.UserId == currentUserId, ct);

        if (worker == null)
        {
            return (null, 403, "El usuario no cuenta con un perfil de trabajador.");
        }

        if (request.WorkerId.HasValue && request.WorkerId.Value != worker.Id)
        {
            return (null, 403, "Solo el trabajador asignado a esta solicitud puede modificar su estado.");
        }

        if (request.Status == RequestStatuses.Pendiente)
        {
            if (normalizedStatus == RequestStatuses.Aceptada)
            {
                if (!request.WorkerId.HasValue)
                {
                    using var tx = await _db.Database.BeginTransactionAsync(ct);
                    var affected = await _db.Requests
                        .Where(r => r.Id == requestId && r.WorkerId == null && r.Status == RequestStatuses.Pendiente)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(r => r.WorkerId, worker.Id)
                            .SetProperty(r => r.Status, RequestStatuses.Aceptada), ct);

                    if (affected == 0)
                    {
                        await tx.RollbackAsync(ct);
                        return (null, 409, "La solicitud ya fue tomada por otro trabajador.");
                    }

                    await tx.CommitAsync(ct);
                    request.WorkerId = worker.Id;
                    request.Worker = worker;
                    request.Status = RequestStatuses.Aceptada;
                    return (request, 200, null);
                }

                request.Status = RequestStatuses.Aceptada;
                await _db.SaveChangesAsync(ct);
                return (request, 200, null);
            }

            if (normalizedStatus == RequestStatuses.Rechazada)
            {
                if (!request.WorkerId.HasValue)
                {
                    return (null, 400, "No se puede rechazar una solicitud que no ha sido asignada.");
                }

                request.WorkerId = null;
                request.Worker = null;
                request.Status = RequestStatuses.Pendiente;
                await _db.SaveChangesAsync(ct);
                return (request, 200, null);
            }

            return (null, 400, "Transición no permitida para una solicitud en estado Pendiente.");
        }

        if (request.Status == RequestStatuses.Aceptada)
        {
            if (normalizedStatus == RequestStatuses.Completada)
            {
                request.Status = RequestStatuses.Completada;
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

                await _db.SaveChangesAsync(ct);
                return (request, 200, null);
            }

            if (normalizedStatus == RequestStatuses.Rechazada)
            {
                request.WorkerId = null;
                request.Worker = null;
                request.Status = RequestStatuses.Pendiente;
                await _db.SaveChangesAsync(ct);
                return (request, 200, null);
            }

            return (null, 400, "Transición no permitida para una solicitud en estado Aceptada.");
        }

        return (null, 400, "Transición de estado no válida.");
    }

    public Task<Worker?> GetWorkerByUserIdAsync(int userId, CancellationToken ct = default)
    {
        return _db.Workers
            .Include(w => w.User)
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);
    }
}
