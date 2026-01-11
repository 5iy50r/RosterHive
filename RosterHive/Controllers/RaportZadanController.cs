using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RosterHive.Data;
using RosterHive.Models;
using RosterHive.ViewModels;

namespace RosterHive.Controllers;

[Authorize(Roles = "Admin")]
public class RaportZadanController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public RaportZadanController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? teamId, DateTime? from, DateTime? to, TaskItemStatus? status, string? assignedToUserId)
    {
        var teams = await _db.Teams
            .OrderBy(t => t.Name)
            .Select(t => new TaskReportViewModel.TeamOption { Id = t.Id, Name = t.Name })
            .ToListAsync();

        if (teams.Count == 0)
            return View(new TaskReportViewModel());

        var selectedTeamId = teamId ?? teams[0].Id;

        var fromDate = (from ?? DateTime.Today.AddDays(-30)).Date;
        var toDate = (to ?? DateTime.Today).Date;

        if (toDate < fromDate)
        {
            var tmp = fromDate;
            fromDate = toDate;
            toDate = tmp;
        }

        var fromDt = fromDate;
        var toExclusive = toDate.AddDays(1);

        var teamOwnerId = await _db.Teams
            .Where(t => t.Id == selectedTeamId)
            .Select(t => t.OwnerUserId)
            .FirstOrDefaultAsync();

        var memberIds = await _db.TeamMembers
            .Where(tm => tm.TeamId == selectedTeamId)
            .Select(tm => tm.UserId)
            .Distinct()
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(teamOwnerId) && !memberIds.Contains(teamOwnerId))
            memberIds.Add(teamOwnerId);

        var users = await _db.Users
            .Where(u => memberIds.Contains(u.Id))
            .OrderBy(u => u.Email)
            .Select(u => new TaskReportViewModel.UserOption
            {
                UserId = u.Id,
                Email = (u.Email ?? u.UserName ?? "Użytkownik")
            })
            .ToListAsync();

        var q = _db.TaskItems
            .Where(t => t.TeamId == selectedTeamId)
            .Where(t => (t.DueDate ?? t.CreatedAt) >= fromDt && (t.DueDate ?? t.CreatedAt) < toExclusive);

        if (status.HasValue)
            q = q.Where(t => t.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(assignedToUserId))
        {
            if (assignedToUserId == "__unassigned__")
                q = q.Where(t => t.AssignedToUserId == null);
            else
                q = q.Where(t => t.AssignedToUserId == assignedToUserId);
        }

        var tasks = await q
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .ToListAsync();

        var assigneeIds = tasks.Where(t => t.AssignedToUserId != null).Select(t => t.AssignedToUserId!).Distinct().ToList();
        var assigneeEmailMap = await _db.Users
            .Where(u => assigneeIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.Email ?? u.UserName ?? "Użytkownik"));

        var today = DateTime.Today;

        var rows = tasks.Select(t =>
        {
            var assignee = t.AssignedToUserId == null
                ? "Nieprzypisane"
                : (assigneeEmailMap.ContainsKey(t.AssignedToUserId) ? assigneeEmailMap[t.AssignedToUserId] : "Użytkownik");

            var completed = t.Status == TaskItemStatus.Zakonczone;
            var hasDue = t.DueDate.HasValue;

            var completedOnTime = completed && hasDue && t.CompletedAt.HasValue && t.CompletedAt.Value <= t.DueDate!.Value;
            var completedLate = completed && hasDue && t.CompletedAt.HasValue && t.CompletedAt.Value > t.DueDate!.Value;
            var completedNoDue = completed && !hasDue;

            var overdueOpen = !completed && hasDue && t.DueDate!.Value.Date < today;

            return new TaskReportViewModel.TaskRow
            {
                Id = t.Id,
                Title = t.Title,
                Priority = t.Priority,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                DueDate = t.DueDate,
                CompletedAt = t.CompletedAt,
                Assignee = assignee,
                IsOverdueOpen = overdueOpen,
                IsCompletedOnTime = completedOnTime,
                IsCompletedLate = completedLate,
                IsCompletedNoDueDate = completedNoDue
            };
        }).ToList();

        var summary = rows
            .GroupBy(r => r.Assignee)
            .Select(g =>
            {
                var total = g.Count();
                var newCount = g.Count(x => x.Status == TaskItemStatus.Nowe);
                var inProgress = g.Count(x => x.Status == TaskItemStatus.Wtoku);
                var completed = g.Count(x => x.Status == TaskItemStatus.Zakonczone);
                var canceled = g.Count(x => x.Status == TaskItemStatus.Anulowane);

                var completedOnTime = g.Count(x => x.IsCompletedOnTime);
                var completedLate = g.Count(x => x.IsCompletedLate);
                var completedNoDue = g.Count(x => x.IsCompletedNoDueDate);

                var overdueOpen = g.Count(x => x.IsOverdueOpen);
                var noDueOpen = g.Count(x => x.Status != TaskItemStatus.Zakonczone && x.DueDate == null);

                return new TaskReportViewModel.SummaryRow
                {
                    Assignee = g.Key,
                    Total = total,
                    NewCount = newCount,
                    InProgressCount = inProgress,
                    CompletedCount = completed,
                    CanceledCount = canceled,
                    CompletedOnTime = completedOnTime,
                    CompletedLate = completedLate,
                    CompletedNoDueDate = completedNoDue,
                    OverdueOpen = overdueOpen,
                    NoDueDateOpen = noDueOpen
                };
            })
            .OrderBy(x => x.Assignee)
            .ToList();

        var vm = new TaskReportViewModel
        {
            Teams = teams,
            Users = users,
            TeamId = selectedTeamId,
            From = fromDate,
            To = toDate,
            Status = status,
            AssignedToUserId = assignedToUserId,
            Tasks = rows,
            Summary = summary,
            TotalCount = rows.Count,
            CompletedCount = rows.Count(x => x.Status == TaskItemStatus.Zakonczone),
            CompletedOnTimeCount = rows.Count(x => x.IsCompletedOnTime),
            CompletedLateCount = rows.Count(x => x.IsCompletedLate),
            CompletedNoDueDateCount = rows.Count(x => x.IsCompletedNoDueDate),
            OverdueOpenCount = rows.Count(x => x.IsOverdueOpen),
            NoDueDateOpenCount = rows.Count(x => x.Status != TaskItemStatus.Zakonczone && x.DueDate == null)
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> EksportCsv(int teamId, DateTime? from, DateTime? to, TaskItemStatus? status, string? assignedToUserId)
    {
        var fromDate = (from ?? DateTime.Today.AddDays(-30)).Date;
        var toDate = (to ?? DateTime.Today).Date;

        if (toDate < fromDate)
        {
            var tmp = fromDate;
            fromDate = toDate;
            toDate = tmp;
        }

        var fromDt = fromDate;
        var toExclusive = toDate.AddDays(1);

        var q = _db.TaskItems
            .Where(t => t.TeamId == teamId)
            .Where(t => (t.DueDate ?? t.CreatedAt) >= fromDt && (t.DueDate ?? t.CreatedAt) < toExclusive);

        if (status.HasValue)
            q = q.Where(t => t.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(assignedToUserId))
        {
            if (assignedToUserId == "__unassigned__")
                q = q.Where(t => t.AssignedToUserId == null);
            else
                q = q.Where(t => t.AssignedToUserId == assignedToUserId);
        }

        var tasks = await q
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .ToListAsync();

        var assigneeIds = tasks.Where(t => t.AssignedToUserId != null).Select(t => t.AssignedToUserId!).Distinct().ToList();
        var assigneeEmailMap = await _db.Users
            .Where(u => assigneeIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.Email ?? u.UserName ?? "Użytkownik"));

        static string Esc(string? v)
        {
            v ??= "";
            v = v.Replace("\"", "\"\"");
            return $"\"{v}\"";
        }

        var today = DateTime.Today;

        var sb = new StringBuilder();
        sb.AppendLine("Id;Tytul;Priorytet;Status;PrzypisaneDo;Utworzono;Termin;Zakonczono;CzyPoTerminie;CzyZalegleOtwarte");

        foreach (var t in tasks)
        {
            var assignee = t.AssignedToUserId == null
                ? "Nieprzypisane"
                : (assigneeEmailMap.ContainsKey(t.AssignedToUserId) ? assigneeEmailMap[t.AssignedToUserId] : "Użytkownik");

            var completed = t.Status == TaskItemStatus.Zakonczone;
            var hasDue = t.DueDate.HasValue;

            var isLate = completed && hasDue && t.CompletedAt.HasValue && t.CompletedAt.Value > t.DueDate!.Value;
            var overdueOpen = !completed && hasDue && t.DueDate!.Value.Date < today;

            sb.AppendLine(string.Join(";",
                Esc(t.Id.ToString()),
                Esc(t.Title),
                Esc(t.Priority.ToString()),
                Esc(t.Status.ToString()),
                Esc(assignee),
                Esc(t.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")),
                Esc(t.DueDate.HasValue ? t.DueDate.Value.ToString("yyyy-MM-dd") : ""),
                Esc(t.CompletedAt.HasValue ? t.CompletedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : ""),
                Esc(isLate ? "Tak" : "Nie"),
                Esc(overdueOpen ? "Tak" : "Nie")
            ));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"raport-zadan-team-{teamId}-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }
}
