using System.ComponentModel.DataAnnotations;

namespace RosterHive.Models;

public class ShiftAssignment
{
    public int Id { get; set; }

    [Required]
    public int ShiftId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public Shift Shift { get; set; } = null!;
}
