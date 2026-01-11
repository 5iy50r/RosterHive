using System.ComponentModel.DataAnnotations;

namespace RosterHive.Models;

public enum ShiftSwapStatus
{
    Oczekuje = 1,
    Przyjete = 2,
    Zatwierdzone = 3,
    Odrzucone = 4,
    Anulowane = 5
}

public class ShiftSwapRequest
{
    public int Id { get; set; }

    [Required]
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    [Required]
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;

    [Required]
    public string RequesterUserId { get; set; } = string.Empty;

    public string? RequestedToUserId { get; set; }

    public string? TakenByUserId { get; set; }

    [Required]
    public ShiftSwapStatus Status { get; set; } = ShiftSwapStatus.Oczekuje;

    [StringLength(500)]
    public string? Message { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? TakenAt { get; set; }

    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByUserId { get; set; }

    [StringLength(500)]
    public string? ReviewNote { get; set; }

    public ICollection<ShiftSwapRequestEvent> Events { get; set; } = new List<ShiftSwapRequestEvent>();
}
