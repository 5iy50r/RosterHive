using System.ComponentModel.DataAnnotations;

namespace RosterHive.Models;

public enum TimeOffEventAction
{
    Utworzono = 1,
    Zatwierdzono = 2,
    Odrzucono = 3,
    Anulowano = 4
}

public class TimeOffRequestEvent
{
    public int Id { get; set; }

    [Required]
    public int TimeOffRequestId { get; set; }
    public TimeOffRequest TimeOffRequest { get; set; } = null!;

    [Required]
    public string ActorUserId { get; set; } = string.Empty;

    [Required]
    public TimeOffEventAction Action { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
