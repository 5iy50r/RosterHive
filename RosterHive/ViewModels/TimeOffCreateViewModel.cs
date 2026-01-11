using System.ComponentModel.DataAnnotations;
using RosterHive.Models;

namespace RosterHive.ViewModels;

public class TimeOffCreateViewModel
{
    [Required]
    public int TeamId { get; set; }

    [Required]
    public TimeOffType Type { get; set; }

    [Required(ErrorMessage = "Podaj datę rozpoczęcia.")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "Podaj datę zakończenia.")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    [StringLength(500, ErrorMessage = "Uzasadnienie może mieć maksymalnie 500 znaków.")]
    public string? Reason { get; set; }

    public List<TeamOption> Teams { get; set; } = new();

    public class TeamOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
