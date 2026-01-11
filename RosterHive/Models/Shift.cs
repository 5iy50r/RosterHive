using System.ComponentModel.DataAnnotations;

namespace RosterHive.Models;

public class Shift
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    [Required]
    public DateTime Start { get; set; }

    [Required]
    public DateTime End { get; set; }

    [StringLength(80)]
    public string? Location { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }

    public ICollection<ShiftAssignment> Assignments { get; set; } = new List<ShiftAssignment>();
}
