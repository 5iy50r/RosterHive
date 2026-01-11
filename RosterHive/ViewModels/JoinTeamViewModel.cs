using System.ComponentModel.DataAnnotations;

namespace RosterHive.ViewModels;

public class JoinTeamViewModel
{
    [Required(ErrorMessage = "Kod dołączenia jest wymagany.")]
    [StringLength(20, ErrorMessage = "Kod dołączenia może mieć maksymalnie 20 znaków.")]
    [Display(Name = "Kod dołączenia")]
    public string JoinCode { get; set; } = string.Empty;
}
