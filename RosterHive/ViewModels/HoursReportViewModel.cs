namespace RosterHive.ViewModels;

public class HoursReportViewModel
{
    public int? TeamId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public List<TeamOption> Teams { get; set; } = new();
    public List<Row> Rows { get; set; } = new();

    public double TotalHours { get; set; }

    public class TeamOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class Row
    {
        public string Email { get; set; } = string.Empty;
        public double Hours { get; set; }
    }
}
