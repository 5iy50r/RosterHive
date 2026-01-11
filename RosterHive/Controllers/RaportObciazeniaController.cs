using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RosterHive.Data;
using RosterHive.ViewModels;

namespace RosterHive.Controllers;

[Authorize(Roles = "Admin")]
public class RaportObciazeniaController : Controller
{
    private readonly ApplicationDbContext _db;

    public RaportObciazeniaController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? teamId, DateTime? from, DateTime? to)
    {
        var teams = await _db.Teams
            .OrderBy(t => t.Name)
            .Select(t => new TeamLoadReportViewModel.TeamOption { Id = t.Id, Name = t.Name })
            .ToListAsync();

        if (teams.Count == 0)
            return View(new TeamLoadReportViewModel());

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

        var shifts = await _db.Shifts
            .Where(s => s.TeamId == selectedTeamId)
            .Where(s => s.Start >= fromDt && s.Start < toExclusive)
            .Include(s => s.Assignments)
            .OrderBy(s => s.Start)
            .ToListAsync();

        var totalShifts = shifts.Count;
        var staffedShifts = shifts.Count(s => s.Assignments.Count > 0);
        var openShifts = totalShifts - staffedShifts;
        var totalAssignments = shifts.Sum(s => s.Assignments.Count);

        var avgPerShift = totalShifts == 0 ? 0 : (double)totalAssignments / totalShifts;
        var avgPerStaffed = staffedShifts == 0 ? 0 : (double)totalAssignments / staffedShifts;

        var openList = shifts
            .Where(s => s.Assignments.Count == 0)
            .Select(s => new TeamLoadReportViewModel.OpenShiftRow
            {
                ShiftId = s.Id,
                Start = s.Start,
                End = s.End,
                Location = s.Location,
                Note = s.Note
            })
            .ToList();

        var vm = new TeamLoadReportViewModel
        {
            Teams = teams,
            TeamId = selectedTeamId,
            From = fromDate,
            To = toDate,
            TotalShifts = totalShifts,
            StaffedShifts = staffedShifts,
            OpenShifts = openShifts,
            TotalAssignments = totalAssignments,
            AvgPeoplePerShift = avgPerShift,
            AvgPeoplePerStaffedShift = avgPerStaffed,
            OpenShiftList = openList
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> EksportCsv(int teamId, DateTime? from, DateTime? to)
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

        var shifts = await _db.Shifts
            .Where(s => s.TeamId == teamId)
            .Where(s => s.Start >= fromDt && s.Start < toExclusive)
            .Include(s => s.Assignments)
            .OrderBy(s => s.Start)
            .ToListAsync();

        var totalShifts = shifts.Count;
        var staffedShifts = shifts.Count(s => s.Assignments.Count > 0);
        var openShifts = totalShifts - staffedShifts;
        var totalAssignments = shifts.Sum(s => s.Assignments.Count);

        var avgPerShift = totalShifts == 0 ? 0 : (double)totalAssignments / totalShifts;
        var avgPerStaffed = staffedShifts == 0 ? 0 : (double)totalAssignments / staffedShifts;

        static string Esc(string? v)
        {
            v ??= "";
            v = v.Replace("\"", "\"\"");
            return $"\"{v}\"";
        }

        var sb = new StringBuilder();
        sb.AppendLine("Metryka;Wartosc");
        sb.AppendLine($"\"Liczba zmian\";\"{totalShifts}\"");
        sb.AppendLine($"\"Zmiany obsadzone\";\"{staffedShifts}\"");
        sb.AppendLine($"\"Zmiany otwarte\";\"{openShifts}\"");
        sb.AppendLine($"\"Łącznie przypisań\";\"{totalAssignments}\"");
        sb.AppendLine($"\"Średnia osób na zmianę\";\"{avgPerShift:0.##}\"");
        sb.AppendLine($"\"Średnia osób na obsadzoną zmianę\";\"{avgPerStaffed:0.##}\"");
        sb.AppendLine();

        sb.AppendLine("ShiftId;Start;End;Location;Note;AssignmentsCount;IsOpen");

        foreach (var s in shifts)
        {
            var isOpen = s.Assignments.Count == 0 ? "Tak" : "Nie";

            sb.AppendLine(string.Join(";",
                Esc(s.Id.ToString()),
                Esc(s.Start.ToString("yyyy-MM-dd HH:mm")),
                Esc(s.End.ToString("yyyy-MM-dd HH:mm")),
                Esc(s.Location),
                Esc(s.Note),
                Esc(s.Assignments.Count.ToString()),
                Esc(isOpen)
            ));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"raport-obciazenia-team-{teamId}-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.csv";

        return File(bytes, "text/csv; charset=utf-8", fileName);
    }
}
