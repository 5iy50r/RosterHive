using System.ComponentModel.DataAnnotations;

namespace RosterHive.ViewModels;

public class ShiftSwapCreateViewModel
{
    [Required]
    public int TeamId { get; set; }

    [Required(ErrorMessage = "Wybierz zmianę do podmiany.")]
    public int ShiftId { get; set; }

    public string? RequestedToUserId { get; set; }

    [StringLength(500, ErrorMessage = "Wiadomość może mieć maksymalnie 500 znaków.")]
    public string? Message { get; set; }

    public List<TeamOption> Teams { get; set; } = new();
    public List<ShiftOption> Shifts { get; set; } = new();
    public List<UserOption> Users { get; set; } = new();

    public class TeamOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class ShiftOption
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class UserOption
    {
        public string UserId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
