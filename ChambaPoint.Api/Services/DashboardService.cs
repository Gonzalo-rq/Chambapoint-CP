using System.Globalization;
using ChambaPoint.Api.Data;
using ChambaPoint.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChambaPoint.Api.Services;

public interface IDashboardService
{
    Task<(object? Dashboard, int StatusCode, string? Error)> GetWorkerDashboardAsync(int userId, CancellationToken ct = default);
}

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(object? Dashboard, int StatusCode, string? Error)> GetWorkerDashboardAsync(int userId, CancellationToken ct = default)
    {
        var worker = await _db.Workers
            .Include(w => w.User)
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);

        if (worker == null)
        {
            return (null, 403, "El usuario autenticado no cuenta con perfil de trabajador.");
        }

        var culture = new CultureInfo("es-PE");
        var now = DateTime.UtcNow;
        var today = now.Date;

        // Calcular inicio de semana (Lunes) y fin de semana (Domingo)
        int diffToMonday = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        var startOfWeek = today.AddDays(-diffToMonday);
        var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);

        // 1. Solicitudes completadas de la semana para ingresos
        var completedRequestsThisWeek = await _db.Requests
            .Where(r => r.WorkerId == worker.Id &&
                        r.Status == RequestStatuses.Completada &&
                        ((r.CompletedAt.HasValue && r.CompletedAt >= startOfWeek && r.CompletedAt <= endOfWeek) ||
                         (!r.CompletedAt.HasValue && r.CreatedAt >= startOfWeek && r.CreatedAt <= endOfWeek)))
            .ToListAsync(ct);

        decimal weeklyTotal = completedRequestsThisWeek.Sum(r => r.Price ?? 150m);

        // 2. Gráfico Lun-Dom
        var dayNames = new[] { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" };
        var weeklyChart = new List<object>();

        for (int i = 0; i < 7; i++)
        {
            var currentDayDate = startOfWeek.AddDays(i);
            var nextDayDate = currentDayDate.AddDays(1);

            var dayJobs = completedRequestsThisWeek.Where(r =>
            {
                var d = (r.CompletedAt ?? r.CreatedAt).Date;
                return d == currentDayDate;
            }).ToList();

            var dayAmount = dayJobs.Sum(r => r.Price ?? 150m);

            weeklyChart.Add(new
            {
                day = dayNames[i],
                date = currentDayDate.ToString("yyyy-MM-dd"),
                amount = dayAmount,
                formatted = "S/ " + dayAmount.ToString("N2", culture),
                completedJobs = dayJobs.Count
            });
        }

        // 3. Contadores
        var pendingRequestsCount = await _db.Requests
            .CountAsync(r => (r.WorkerId == worker.Id || (r.WorkerId == null && r.Category == worker.Profession)) &&
                             r.Status == RequestStatuses.Pendiente, ct);

        var confirmedRequestsCount = await _db.Requests
            .CountAsync(r => r.WorkerId == worker.Id && r.Status == RequestStatuses.Aceptada, ct);

        var completedRequestsCount = await _db.Requests
            .CountAsync(r => r.WorkerId == worker.Id && r.Status == RequestStatuses.Completada, ct);

        var pendingAppointmentsCount = await _db.Appointments
            .CountAsync(a => a.WorkerId == worker.Id && a.Status == AppointmentStatuses.Nueva, ct);

        var confirmedAppointmentsCount = await _db.Appointments
            .CountAsync(a => a.WorkerId == worker.Id && a.Status == AppointmentStatuses.Aceptada, ct);

        // 4. Citas pendientes con botones Aceptar/Rechazar
        var pendingAppointments = await _db.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Request)
            .Where(a => a.WorkerId == worker.Id && a.Status == AppointmentStatuses.Nueva)
            .OrderBy(a => a.DateTime)
            .Take(10)
            .ToListAsync(ct);

        var pendingAppointmentsDto = pendingAppointments.Select(a => new
        {
            a.Id,
            a.RequestId,
            title = !string.IsNullOrWhiteSpace(a.Request?.Description) ? a.Request.Description : a.Description,
            category = a.Request?.Category ?? worker.Profession,
            customerName = a.Customer?.Name,
            customerAvatar = a.Customer?.AvatarUrl,
            dateTime = a.DateTime,
            formattedDate = a.DateTime.ToString("dd/MM/yyyy HH:mm", culture),
            description = a.Description,
            status = a.Status,
            actions = new
            {
                accept = new
                {
                    method = "PATCH",
                    url = $"/api/appointments/{a.Id}/status",
                    body = new { status = "Aceptada" }
                },
                reject = new
                {
                    method = "PATCH",
                    url = $"/api/appointments/{a.Id}/status",
                    body = new { status = "Rechazada" }
                }
            }
        });

        // 5. Actividad reciente
        var recentRequests = await _db.Requests
            .Include(r => r.Customer)
            .Where(r => r.WorkerId == worker.Id)
            .OrderByDescending(r => r.CompletedAt ?? r.CreatedAt)
            .Take(5)
            .ToListAsync(ct);

        var recentActivityDto = recentRequests.Select(r =>
        {
            var date = r.CompletedAt ?? r.CreatedAt;
            var timeSpan = now - date;
            string timeAgo = timeSpan.TotalHours < 1 ? "Hace unos minutos" :
                             timeSpan.TotalHours < 24 ? $"Hace {(int)timeSpan.TotalHours} horas" :
                             $"Hace {(int)timeSpan.TotalDays} días";

            var price = r.Price ?? (r.Status == RequestStatuses.Completada ? 150m : 0m);

            return new
            {
                id = r.Id,
                type = "request",
                title = r.Description,
                subtitle = $"{timeAgo} • {r.Category}",
                amount = price,
                amountFormatted = "S/ " + price.ToString("N2", culture),
                status = r.Status,
                timestamp = date
            };
        });

        var dashboard = new
        {
            worker = new
            {
                worker.Id,
                worker.UserId,
                name = worker.User?.Name,
                worker.Profession,
                worker.ExperienceYears,
                worker.JobsCount,
                avatarUrl = worker.User?.AvatarUrl,
                worker.IsOnline
            },
            weeklyEarnings = new
            {
                total = weeklyTotal,
                formatted = "S/ " + weeklyTotal.ToString("N2", culture),
                currency = "PEN",
                period = $"{startOfWeek:dd MMM} - {endOfWeek:dd MMM}"
            },
            weeklyChart,
            counters = new
            {
                pendingRequests = pendingRequestsCount,
                confirmedRequests = confirmedRequestsCount,
                completedRequests = completedRequestsCount,
                pendingAppointments = pendingAppointmentsCount,
                confirmedAppointments = confirmedAppointmentsCount
            },
            pendingAppointments = pendingAppointmentsDto,
            recentActivity = recentActivityDto
        };

        return (dashboard, 200, null);
    }
}
