namespace RosterHive.ViewModels;

public class TeamLoadReportViewModel
{
    public int TeamId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    public List<TeamOption> Teams { get; set; } = new();

    public int TotalShifts { get; set; }
    public int StaffedShifts { get; set; }
    public int OpenShifts { get; set; }
    public int TotalAssignments { get; set; }

    public double AvgPeoplePerShift { get; set; }
    public double AvgPeoplePerStaffedShift { get; set; }

    public List<OpenShiftRow> OpenShiftList { get; set; } = new();

    public class TeamOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class OpenShiftRow
    {
        public int ShiftId { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string? Location { get; set; }
        public string? Note { get; set; }
    }
}
