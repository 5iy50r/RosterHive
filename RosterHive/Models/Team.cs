using System.ComponentModel.DataAnnotations;

namespace RosterHive.Models;

public class Team
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Nazwa zespołu jest wymagana.")]
    [StringLength(80, ErrorMessage = "Nazwa zespołu może mieć maksymalnie 80 znaków.")]
    [Display(Name = "Nazwa zespołu")]
    public string Name { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "Opis może mieć maksymalnie 300 znaków.")]
    [Display(Name = "Opis")]
    public string? Description { get; set; }

    [Required]
    [StringLength(20)]
    [Display(Name = "Kod dołączenia")]
    public string JoinCode { get; set; } = string.Empty;

    [Required]
    public string OwnerUserId { get; set; } = string.Empty;

    [Display(Name = "Data utworzenia")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
}
