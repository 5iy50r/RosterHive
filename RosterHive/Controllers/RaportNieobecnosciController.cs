using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RosterHive.Data;
using RosterHive.Models;
using RosterHive.ViewModels;

namespace RosterHive.Controllers;

[Authorize]
public class RaportNieobecnosciController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public RaportNieobecnosciController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? teamId, DateTime? from, DateTime? to, TimeOffStatus? status)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var isAdmin = User.IsInRole("Admin");

        var teamsQuery = _db.Teams.AsQueryable();

        if (!isAdmin)
        {
            teamsQuery = teamsQuery.Where(t =>
                t.OwnerUserId == userId ||
                _db.TeamMembers.Any(tm => tm.TeamId == t.Id && tm.UserId == userId));
        }

        var teams = await teamsQuery
            .OrderBy(t => t.Name)
            .Select(t => new AbsenceReportViewModel.TeamOption { Id = t.Id, Name = t.Name })
            .ToListAsync();

        if (teams.Count == 0)
            return Forbid();

        var selectedTeamId = teamId ?? teams[0].Id;

        if (!isAdmin)
        {
            var allowed = await _db.Teams.AnyAsync(t => t.Id == selectedTeamId && (t.OwnerUserId == userId ||
                _db.TeamMembers.Any(tm => tm.TeamId == t.Id && tm.UserId == userId)));

            if (!allowed)
                return Forbid();
        }

        var fromDate = (from ?? DateTime.Today.AddDays(-30)).Date;
        var toDate = (to ?? DateTime.Today).Date;

        if (toDate < fromDate)
        {
            var tmp = fromDate;
            fromDate = toDate;
            toDate = tmp;
        }

        var q = _db.TimeOffRequests
            .Where(r => r.TeamId == selectedTeamId)
            .Where(r => r.StartDate.Date <= toDate && r.EndDate.Date >= fromDate);

        if (status.HasValue)
            q = q.Where(r => r.Status == status.Value);

        var requests = await q
            .OrderByDescending(r => r.StartDate)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync();

        var requesterIds = requests.Select(r => r.RequesterUserId).Distinct().ToList();
        var reviewerIds = requests.Where(r => r.ReviewedByUserId != null).Select(r => r.ReviewedByUserId!).Distinct().ToList();

        var allUserIds = requesterIds.Concat(reviewerIds).Distinct().ToList();

        var userEmailMap = await _db.Users
            .Where(u => allUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.Email ?? u.UserName ?? "Użytkownik"));

        double DaysInRange(DateTime start, DateTime end)
        {
            var s = start.Date < fromDate ? fromDate : start.Date;
            var e = end.Date > toDate ? toDate : end.Date;
            if (e < s) return 0;
            return (e - s).TotalDays + 1;
        }

        var requestRows = requests.Select(r =>
        {
            var email = userEmailMap.ContainsKey(r.RequesterUserId) ? userEmailMap[r.RequesterUserId] : "Użytkownik";
            var reviewer = r.ReviewedByUserId != null && userEmailMap.ContainsKey(r.ReviewedByUserId) ? userEmailMap[r.ReviewedByUserId] : null;

            return new AbsenceReportViewModel.RequestRow
            {
                Id = r.Id,
                Email = email,
                Type = r.Type,
                StartDate = r.StartDate.Date,
                EndDate = r.EndDate.Date,
                DaysInRange = DaysInRange(r.StartDate, r.EndDate),
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                Reason = r.Reason,
                ReviewedAt = r.ReviewedAt,
                ReviewedBy = reviewer,
                ReviewNote = r.ReviewNote
            };
        }).ToList();

        var summary = requestRows
            .GroupBy(r => r.Email)
            .Select(g =>
            {
                var pending = g.Count(x => x.Status == TimeOffStatus.Oczekuje);
                var approved = g.Count(x => x.Status == TimeOffStatus.Zatwierdzony);
                var rejected = g.Count(x => x.Status == TimeOffStatus.Odrzucony);
                var canceled = g.Count(x => x.Status == TimeOffStatus.Anulowany);

                var approvedDays = g.Where(x => x.Status == TimeOffStatus.Zatwierdzony).Sum(x => x.DaysInRange);
                var totalDays = g.Sum(x => x.DaysInRange);

                return new AbsenceReportViewModel.SummaryRow
                {
                    UserId = "",
                    Email = g.Key,
                    PendingCount = pending,
                    ApprovedCount = approved,
                    RejectedCount = rejected,
                    CanceledCount = canceled,
                    ApprovedDays = approvedDays,
                    TotalDays = totalDays
                };
            })
            .OrderBy(x => x.Email)
            .ToList();

        var vm = new AbsenceReportViewModel
        {
            Teams = teams,
            TeamId = selectedTeamId,
            From = fromDate,
            To = toDate,
            Status = status,
            Summary = summary,
            Requests = requestRows,
            TotalCount = requestRows.Count,
            TotalDays = requestRows.Sum(x => x.DaysInRange),
            TotalApprovedDays = requestRows.Where(x => x.Status == TimeOffStatus.Zatwierdzony).Sum(x => x.DaysInRange)
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> EksportCsv(int teamId, DateTime? from, DateTime? to, TimeOffStatus? status)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var isAdmin = User.IsInRole("Admin");

        if (!isAdmin)
        {
            var allowed = await _db.Teams.AnyAsync(t => t.Id == teamId && (t.OwnerUserId == userId ||
                _db.TeamMembers.Any(tm => tm.TeamId == t.Id && tm.UserId == userId)));

            if (!allowed)
                return Forbid();
        }

        var fromDate = (from ?? DateTime.Today.AddDays(-30)).Date;
        var toDate = (to ?? DateTime.Today).Date;

        if (toDate < fromDate)
        {
            var tmp = fromDate;
            fromDate = toDate;
            toDate = tmp;
        }

        var q = _db.TimeOffRequests
            .Where(r => r.TeamId == teamId)
            .Where(r => r.StartDate.Date <= toDate && r.EndDate.Date >= fromDate);

        if (status.HasValue)
            q = q.Where(r => r.Status == status.Value);

        var requests = await q
            .OrderByDescending(r => r.StartDate)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync();

        var requesterIds = requests.Select(r => r.RequesterUserId).Distinct().ToList();
        var reviewerIds = requests.Where(r => r.ReviewedByUserId != null).Select(r => r.ReviewedByUserId!).Distinct().ToList();
        var allUserIds = requesterIds.Concat(reviewerIds).Distinct().ToList();

        var userEmailMap = await _db.Users
            .Where(u => allUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.Email ?? u.UserName ?? "Użytkownik"));

        double DaysInRange(DateTime start, DateTime end)
        {
            var s = start.Date < fromDate ? fromDate : start.Date;
            var e = end.Date > toDate ? toDate : end.Date;
            if (e < s) return 0;
            return (e - s).TotalDays + 1;
        }

        static string Esc(string? v)
        {
            v ??= "";
            v = v.Replace("\"", "\"\"");
            return $"\"{v}\"";
        }

        var sb = new StringBuilder();
        sb.AppendLine("Email;Typ;Od;Do;DniWZakresie;Status;Utworzono;Powod;Rozpatrzono;Rozpatrzyl;Notatka");

        foreach (var r in requests)
        {
            var email = userEmailMap.ContainsKey(r.RequesterUserId) ? userEmailMap[r.RequesterUserId] : "Użytkownik";
            var reviewer = r.ReviewedByUserId != null && userEmailMap.ContainsKey(r.ReviewedByUserId) ? userEmailMap[r.ReviewedByUserId] : "";

            var line = string.Join(";",
                Esc(email),
                Esc(r.Type.ToString()),
                Esc(r.StartDate.Date.ToString("yyyy-MM-dd")),
                Esc(r.EndDate.Date.ToString("yyyy-MM-dd")),
                Esc(DaysInRange(r.StartDate, r.EndDate).ToString("0.##")),
                Esc(r.Status.ToString()),
                Esc(r.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")),
                Esc(r.Reason),
                Esc(r.ReviewedAt.HasValue ? r.ReviewedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : ""),
                Esc(reviewer),
                Esc(r.ReviewNote)
            );

            sb.AppendLine(line);
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"raport-nieobecnosci-team-{teamId}-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }
}
