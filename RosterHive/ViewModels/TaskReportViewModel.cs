using RosterHive.Models;

namespace RosterHive.ViewModels;

public class TaskReportViewModel
{
    public int TeamId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public TaskItemStatus? Status { get; set; }
    public string? AssignedToUserId { get; set; }

    public List<TeamOption> Teams { get; set; } = new();
    public List<UserOption> Users { get; set; } = new();

    public List<SummaryRow> Summary { get; set; } = new();
    public List<TaskRow> Tasks { get; set; } = new();

    public int TotalCount { get; set; }
    public int CompletedCount { get; set; }
    public int CompletedOnTimeCount { get; set; }
    public int CompletedLateCount { get; set; }
    public int CompletedNoDueDateCount { get; set; }
    public int OverdueOpenCount { get; set; }
    public int NoDueDateOpenCount { get; set; }

    public class TeamOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class UserOption
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class SummaryRow
    {
        public string Assignee { get; set; } = string.Empty;

        public int Total { get; set; }
        public int NewCount { get; set; }
        public int InProgressCount { get; set; }
        public int CompletedCount { get; set; }
        public int CanceledCount { get; set; }

        public int CompletedOnTime { get; set; }
        public int CompletedLate { get; set; }
        public int CompletedNoDueDate { get; set; }

        public int OverdueOpen { get; set; }
        public int NoDueDateOpen { get; set; }
    }

    public class TaskRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;

        public TaskPriority Priority { get; set; }
        public TaskItemStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }

        public string Assignee { get; set; } = string.Empty;

        public bool IsOverdueOpen { get; set; }
        public bool IsCompletedOnTime { get; set; }
        public bool IsCompletedLate { get; set; }
        public bool IsCompletedNoDueDate { get; set; }
    }
}
