namespace RosterHive.ViewModels;

public class TeamShiftsViewModel
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;

    public bool CanManage { get; set; }

    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public List<Row> Shifts { get; set; } = new();

    public class Row
    {
        public int Id { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string EmployeesLabel { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? Note { get; set; }
    }
}
