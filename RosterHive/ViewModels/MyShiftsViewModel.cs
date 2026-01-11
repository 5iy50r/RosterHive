namespace RosterHive.ViewModels;

public class MyShiftsViewModel
{
    public List<Row> Shifts { get; set; } = new();

    public class Row
    {
        public string TeamName { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string? Note { get; set; }
    }
}
