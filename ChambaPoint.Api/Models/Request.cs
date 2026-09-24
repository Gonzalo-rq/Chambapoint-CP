using System.ComponentModel.DataAnnotations;

namespace ChambaPoint.Api.Models;

public static class RequestCategories
{
    public const string Plomeria = "Plomería";
    public const string Electricidad = "Electricidad";
    public const string Carpinteria = "Carpintería";
    public const string Pintura = "Pintura";

    public static readonly string[] All = { Plomeria, Electricidad, Carpinteria, Pintura };

    public static bool IsValid(string? category) =>
        !string.IsNullOrWhiteSpace(category) &&
        All.Any(c => string.Equals(c, category.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string category) =>
        All.FirstOrDefault(c => string.Equals(c, category.Trim(), StringComparison.OrdinalIgnoreCase)) ?? category;
}

public static class RequestUrgencies
{
    public const string LoAntesPosible = "Lo antes posible";
    public const string Flexible = "Flexible";
    public const string Programar = "Programar";

    public static readonly string[] All = { LoAntesPosible, Flexible, Programar };

    public static bool IsValid(string? urgency) =>
        !string.IsNullOrWhiteSpace(urgency) &&
        All.Any(u => string.Equals(u, urgency.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string urgency) =>
        All.FirstOrDefault(u => string.Equals(u, urgency.Trim(), StringComparison.OrdinalIgnoreCase)) ?? urgency;
}

public static class RequestStatuses
{
    public const string Pendiente = "Pendiente";
    public const string Aceptada = "Aceptada";
    public const string Completada = "Completada";
    public const string Rechazada = "Rechazada";

    public static readonly string[] All = { Pendiente, Aceptada, Completada, Rechazada };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) &&
        All.Any(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string status) =>
        All.FirstOrDefault(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase)) ?? status;
}

public class Request
{
    public int Id { get; set; }

    [Required]
    public int CustomerId { get; set; }

    public User Customer { get; set; } = null!;

    public int? WorkerId { get; set; }

    public Worker? Worker { get; set; }

    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    public List<string> Photos { get; set; } = new();

    [Required, MaxLength(50)]
    public string Urgency { get; set; } = RequestUrgencies.LoAntesPosible;

    public DateTime? ScheduledAt { get; set; }

    [Required, MaxLength(50)]
    public string Status { get; set; } = RequestStatuses.Pendiente;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
