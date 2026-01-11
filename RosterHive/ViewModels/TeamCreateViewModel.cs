using System.ComponentModel.DataAnnotations;

namespace RosterHive.ViewModels;

public class TeamCreateViewModel
{
    [Required(ErrorMessage = "Nazwa zespołu jest wymagana.")]
    [StringLength(80, ErrorMessage = "Nazwa zespołu może mieć maksymalnie 80 znaków.")]
    [Display(Name = "Nazwa zespołu")]
    public string Name { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "Opis może mieć maksymalnie 300 znaków.")]
    [Display(Name = "Opis")]
    public string? Description { get; set; }
}
