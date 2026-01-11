using RosterHive.Models;

namespace RosterHive.ViewModels;

public class AbsenceReportViewModel
{
    public int TeamId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public TimeOffStatus? Status { get; set; }

    public List<TeamOption> Teams { get; set; } = new();
    public List<SummaryRow> Summary { get; set; } = new();
    public List<RequestRow> Requests { get; set; } = new();

    public int TotalCount { get; set; }
    public double TotalDays { get; set; }
    public double TotalApprovedDays { get; set; }

    public class TeamOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class SummaryRow
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int CanceledCount { get; set; }

        public double ApprovedDays { get; set; }
        public double TotalDays { get; set; }
    }

    public class RequestRow
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public TimeOffType Type { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double DaysInRange { get; set; }
        public TimeOffStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Reason { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedBy { get; set; }
        public string? ReviewNote { get; set; }
    }
}
