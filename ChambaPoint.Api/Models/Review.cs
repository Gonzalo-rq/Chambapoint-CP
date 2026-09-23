using System.ComponentModel.DataAnnotations;

namespace ChambaPoint.Api.Models;

public class Review
{
    public int Id { get; set; }

    [Required]
    public int WorkerId { get; set; }

    public Worker Worker { get; set; } = null!;

    [Required]
    public int CustomerId { get; set; }

    public User Customer { get; set; } = null!;

    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(2000)]
    public string Text { get; set; } = string.Empty;

    public List<string> Photos { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}