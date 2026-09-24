using System.ComponentModel.DataAnnotations;

namespace ChambaPoint.Api.Models;

public class Message
{
    public int Id { get; set; }

    [Required]
    public int SenderId { get; set; }

    public User Sender { get; set; } = null!;

    [Required]
    public int ReceiverId { get; set; }

    public User Receiver { get; set; } = null!;

    public int? RequestId { get; set; }

    public Request? Request { get; set; }

    [Required, MaxLength(2000)]
    public string Text { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; } = false;
}
