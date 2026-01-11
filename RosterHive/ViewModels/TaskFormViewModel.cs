using System.ComponentModel.DataAnnotations;
using RosterHive.Models;

namespace RosterHive.ViewModels;

public class TaskFormViewModel
{
    public int? Id { get; set; }

    [Required]
    public int TeamId { get; set; }

    public int? ShiftId { get; set; }

    [Required(ErrorMessage = "Podaj tytuł zadania.")]
    [StringLength(120, ErrorMessage = "Tytuł może mieć maksymalnie 120 znaków.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Opis może mieć maksymalnie 1000 znaków.")]
    public string? Description { get; set; }

    [Required]
    public TaskPriority Priority { get; set; } = TaskPriority.Sredni;

    [Required]
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Nowe;

    public DateTime? DueDate { get; set; }

    public string? AssignedToUserId { get; set; }

    public List<TeamOption> Teams { get; set; } = new();
    public List<UserOption> Users { get; set; } = new();
    public List<ShiftOption> Shifts { get; set; } = new();

    public class TeamOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class UserOption
    {
        public string UserId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class ShiftOption
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
