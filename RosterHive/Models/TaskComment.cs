using System.ComponentModel.DataAnnotations;

namespace RosterHive.Models;

public enum TaskActivityType
{
    Utworzono = 1,
    Komentarz = 2,
    ZmienionoStatus = 3,
    ZmienionoPrzypisanie = 4,
    ZmienionoTermin = 5,
    ZmienionoPriorytet = 6,
    ZmienionoPowiazanieZmiany = 7
}

public class TaskComment
{
    public int Id { get; set; }

    [Required]
    public int TaskItemId { get; set; }
    public TaskItem TaskItem { get; set; } = null!;

    [Required]
    public string AuthorUserId { get; set; } = string.Empty;

    [Required]
    public TaskActivityType Type { get; set; } = TaskActivityType.Komentarz;

    [StringLength(800)]
    public string? Content { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
