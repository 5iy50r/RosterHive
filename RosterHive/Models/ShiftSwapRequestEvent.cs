using System.ComponentModel.DataAnnotations;

namespace RosterHive.Models;

public enum ShiftSwapEventAction
{
    Utworzono = 1,
    Przyjeto = 2,
    Zatwierdzono = 3,
    Odrzucono = 4,
    Anulowano = 5
}

public class ShiftSwapRequestEvent
{
    public int Id { get; set; }

    [Required]
    public int ShiftSwapRequestId { get; set; }
    public ShiftSwapRequest ShiftSwapRequest { get; set; } = null!;

    [Required]
    public string ActorUserId { get; set; } = string.Empty;

    [Required]
    public ShiftSwapEventAction Action { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
