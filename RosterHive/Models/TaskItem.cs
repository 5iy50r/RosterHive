using System.ComponentModel.DataAnnotations;

namespace RosterHive.Models;

public enum TaskPriority
{
    Niski = 1,
    Sredni = 2,
    Wysoki = 3,
    Krytyczny = 4
}

public enum TaskItemStatus
{
    Nowe = 1,
    Wtoku = 2,
    Zakonczone = 3,
    Anulowane = 4
}

public class TaskItem
{
    public int Id { get; set; }

    [Required]
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    [Required]
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public TaskPriority Priority { get; set; } = TaskPriority.Sredni;

    [Required]
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Nowe;

    public DateTime? DueDate { get; set; }

    public string? AssignedToUserId { get; set; }

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
}
