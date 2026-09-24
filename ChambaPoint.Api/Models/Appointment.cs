using System.ComponentModel.DataAnnotations;

namespace ChambaPoint.Api.Models;

public static class AppointmentStatuses
{
    public const string Nueva = "Nueva";
    public const string Aceptada = "Aceptada";
    public const string Rechazada = "Rechazada";
    public const string Completada = "Completada";

    public static readonly string[] All = { Nueva, Aceptada, Rechazada, Completada };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) &&
        All.Any(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string status) =>
        All.FirstOrDefault(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase)) ?? status;
}

public class Appointment
{
    public int Id { get; set; }

    [Required]
    public int RequestId { get; set; }

    public Request Request { get; set; } = null!;

    [Required]
    public int WorkerId { get; set; }

    public Worker Worker { get; set; } = null!;

    [Required]
    public int CustomerId { get; set; }

    public User Customer { get; set; } = null!;

    [Required]
    public DateTime DateTime { get; set; }

    [Required, MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Status { get; set; } = AppointmentStatuses.Nueva;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
