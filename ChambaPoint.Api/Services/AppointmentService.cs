using ChambaPoint.Api.Data;
using ChambaPoint.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChambaPoint.Api.Services;

public record ScheduleAppointmentInput(
    int RequestId,
    int? WorkerId,
    int? CustomerId,
    DateTime DateTime,
    string Description
);

public record UpdateAppointmentStatusInput(
    string Status
);

public interface IAppointmentService
{
    Task<(Appointment? Appointment, int StatusCode, string? Error, int? AffectedUserId)> ScheduleAsync(int currentUserId, string currentUserRole, ScheduleAppointmentInput input, CancellationToken ct = default);
    Task<(Appointment? Appointment, int StatusCode, string? Error, int? AffectedUserId)> UpdateStatusAsync(int appointmentId, int currentUserId, string newStatus, CancellationToken ct = default);
    Task<Appointment?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<(List<Appointment> Items, int Total)> ListAsync(int currentUserId, string? status, int? requestId, int page, int pageSize, CancellationToken ct = default);
}

public class AppointmentService : IAppointmentService
{
    private readonly AppDbContext _db;

    public AppointmentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(Appointment? Appointment, int StatusCode, string? Error, int? AffectedUserId)> ScheduleAsync(
        int currentUserId,
        string currentUserRole,
        ScheduleAppointmentInput input,
        CancellationToken ct = default)
    {
        if (input.RequestId <= 0)
        {
            return (null, 400, "RequestId es requerido.", null);
        }

        if (string.IsNullOrWhiteSpace(input.Description))
        {
            return (null, 400, "La descripción de la cita es requerida.", null);
        }

        if (input.DateTime <= DateTime.UtcNow.AddMinutes(-5))
        {
            return (null, 400, "La fecha de la cita no puede estar en el pasado.", null);
        }

        var request = await _db.Requests
            .Include(r => r.Customer)
            .Include(r => r.Worker!)
                .ThenInclude(w => w.User)
            .FirstOrDefaultAsync(r => r.Id == input.RequestId, ct);

        if (request == null)
        {
            return (null, 404, "Solicitud no encontrada.", null);
        }

        // Determinar Worker y Customer
        int customerId = request.CustomerId;
        int workerId = 0;

        if (request.WorkerId.HasValue)
        {
            workerId = request.WorkerId.Value;
        }
        else if (input.WorkerId.HasValue)
        {
            var workerExists = await _db.Workers.AnyAsync(w => w.Id == input.WorkerId.Value, ct);
            if (!workerExists)
            {
                return (null, 404, "El trabajador especificado no existe.", null);
            }
            workerId = input.WorkerId.Value;
            request.WorkerId = workerId;
        }
        else
        {
            return (null, 400, "Debe especificar un trabajador para agendar la cita.", null);
        }

        var worker = await _db.Workers.Include(w => w.User).FirstOrDefaultAsync(w => w.Id == workerId, ct);
        if (worker == null)
        {
            return (null, 404, "Trabajador no encontrado.", null);
        }

        // Validar que el usuario que agenda sea el cliente o el trabajador de la solicitud
        var isCustomer = customerId == currentUserId;
        var isWorker = worker.UserId == currentUserId;

        if (!isCustomer && !isWorker)
        {
            return (null, 403, "Solo el cliente o el trabajador de la solicitud pueden agendar citas.", null);
        }

        var appointment = new Appointment
        {
            RequestId = request.Id,
            WorkerId = workerId,
            CustomerId = customerId,
            DateTime = input.DateTime,
            Description = input.Description.Trim(),
            Status = AppointmentStatuses.Nueva,
            CreatedAt = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(ct);

        // Cargar relaciones
        await _db.Entry(appointment).Reference(a => a.Customer).LoadAsync(ct);
        await _db.Entry(appointment).Reference(a => a.Worker).Query().Include(w => w.User).LoadAsync(ct);
        await _db.Entry(appointment).Reference(a => a.Request).LoadAsync(ct);

        // El usuario afectado es la contraparte
        int affectedUserId = isCustomer ? worker.UserId : customerId;

        return (appointment, 201, null, affectedUserId);
    }

    public async Task<(Appointment? Appointment, int StatusCode, string? Error, int? AffectedUserId)> UpdateStatusAsync(
        int appointmentId,
        int currentUserId,
        string newStatus,
        CancellationToken ct = default)
    {
        if (!AppointmentStatuses.IsValid(newStatus))
        {
            return (null, 400, $"Estado inválido. Opciones permitidas: {string.Join(", ", AppointmentStatuses.All)}", null);
        }

        var normalizedStatus = AppointmentStatuses.Normalize(newStatus);
        if (normalizedStatus == AppointmentStatuses.Nueva)
        {
            return (null, 400, "No se puede revertir el estado de una cita a Nueva.", null);
        }

        var appointment = await _db.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Worker!)
                .ThenInclude(w => w.User)
            .Include(a => a.Request)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);

        if (appointment == null)
        {
            return (null, 404, "Cita no encontrada.", null);
        }

        var isCustomer = appointment.CustomerId == currentUserId;
        var isWorker = appointment.Worker?.UserId == currentUserId;

        if (!isCustomer && !isWorker)
        {
            return (null, 403, "No tienes permiso para modificar esta cita.", null);
        }

        appointment.Status = normalizedStatus;

        // Si la cita se completa y la solicitud asociada estaba aceptada, opcionalmente actualizar la solicitud
        if (normalizedStatus == AppointmentStatuses.Completada && appointment.Request != null)
        {
            if (appointment.Request.Status == RequestStatuses.Aceptada)
            {
                appointment.Request.Status = RequestStatuses.Completada;
            }
        }

        await _db.SaveChangesAsync(ct);

        int affectedUserId = isCustomer ? appointment.Worker!.UserId : appointment.CustomerId;

        return (appointment, 200, null, affectedUserId);
    }

    public async Task<Appointment?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Worker!)
                .ThenInclude(w => w.User)
            .Include(a => a.Request)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<(List<Appointment> Items, int Total)> ListAsync(
        int currentUserId,
        string? status,
        int? requestId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var worker = await _db.Workers.FirstOrDefaultAsync(w => w.UserId == currentUserId, ct);

        var query = _db.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Worker!)
                .ThenInclude(w => w.User)
            .Include(a => a.Request)
            .Where(a => a.CustomerId == currentUserId || (worker != null && a.WorkerId == worker.Id));

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = AppointmentStatuses.Normalize(status);
            query = query.Where(a => a.Status == normalized);
        }

        if (requestId.HasValue)
        {
            query = query.Where(a => a.RequestId == requestId.Value);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(a => a.DateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
