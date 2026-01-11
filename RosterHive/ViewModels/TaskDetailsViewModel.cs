using RosterHive.Models;

namespace RosterHive.ViewModels;

public class TaskDetailsViewModel
{
    public TaskItem Task { get; set; } = null!;
    public bool CanManage { get; set; }
    public bool CanChangeStatus { get; set; }

    public string? AssignedLabel { get; set; }
    public string? CreatedByLabel { get; set; }

    public List<ActivityRow> Activity { get; set; } = new();

    public class ActivityRow
    {
        public DateTime CreatedAt { get; set; }
        public string AuthorLabel { get; set; } = string.Empty;
        public TaskActivityType Type { get; set; }
        public string? Content { get; set; }
    }
}
