using System.ComponentModel.DataAnnotations;

namespace RosterHive.ViewModels;

public class ShiftFormViewModel
{
    public int? Id { get; set; }

    [Required]
    public int TeamId { get; set; }

    [Required(ErrorMessage = "Podaj datę i godzinę rozpoczęcia.")]
    public DateTime Start { get; set; }

    [Required(ErrorMessage = "Podaj datę i godzinę zakończenia.")]
    public DateTime End { get; set; }

    [StringLength(80, ErrorMessage = "Lokalizacja/obszar może mieć maksymalnie 80 znaków.")]
    [Display(Name = "Lokalizacja/Obszar")]
    public string? Location { get; set; }

    [StringLength(300, ErrorMessage = "Notatka może mieć maksymalnie 300 znaków.")]
    [Display(Name = "Notatka")]
    public string? Note { get; set; }

    public List<string> SelectedUserIds { get; set; } = new();
    public List<EmployeeOption> Employees { get; set; } = new();

    public class EmployeeOption
    {
        public string UserId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
