using System.ComponentModel.DataAnnotations;

namespace RosterHive.Models;

public enum TimeOffType
{
    UrlopWypoczynkowy = 1,
    UrlopNaZadanie = 2,
    ZwolnienieLekarskie = 3,
    Inne = 4
}

public enum TimeOffStatus
{
    Oczekuje = 1,
    Zatwierdzony = 2,
    Odrzucony = 3,
    Anulowany = 4
}

public class TimeOffRequest
{
    public int Id { get; set; }

    [Required]
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    [Required]
    public string RequesterUserId { get; set; } = string.Empty;

    [Required]
    public TimeOffType Type { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    public TimeOffStatus Status { get; set; } = TimeOffStatus.Oczekuje;

    [StringLength(500)]
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByUserId { get; set; }

    [StringLength(500)]
    public string? ReviewNote { get; set; }

    public ICollection<TimeOffRequestEvent> Events { get; set; } = new List<TimeOffRequestEvent>();
}
