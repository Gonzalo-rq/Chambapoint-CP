using System.ComponentModel.DataAnnotations;

namespace ChambaPoint.Api.Models;

public class Worker
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    public User User { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Profession { get; set; } = string.Empty;

    public int ExperienceYears { get; set; }

    public double DistanceKm { get; set; }

    public int JobsCount { get; set; }

    [MaxLength(3000)]
    public string About { get; set; } = string.Empty;

    public List<string> Certifications { get; set; } = new();

    public List<string> Gallery { get; set; } = new();

    public bool IsOnline { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Review> Reviews { get; set; } = new();
}