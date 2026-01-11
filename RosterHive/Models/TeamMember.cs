using System.ComponentModel.DataAnnotations;

namespace RosterHive.Models;

public class TeamMember
{
    public int Id { get; set; }

    [Required]
    public int TeamId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Display(Name = "Data dołączenia")]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Team Team { get; set; } = null!;
}
