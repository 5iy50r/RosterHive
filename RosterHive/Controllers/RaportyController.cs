using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RosterHive.Data;
using RosterHive.ViewModels;

namespace RosterHive.Controllers;

[Authorize(Roles = "Admin")]
public class RaportyController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public RaportyController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Godziny(int? teamId = null, DateTime? from = null, DateTime? to = null)
    {
        var teams = await _db.Teams
            .OrderBy(t => t.Name)
            .Select(t => new HoursReportViewModel.TeamOption
            {
                Id = t.Id,
                Name = t.Name
            })
            .ToListAsync();

        var vm = new HoursReportViewModel
        {
            Teams = teams
        };

        if (teams.Count == 0)
            return View(vm);

        var selectedTeamId = teamId ?? teams[0].Id;

        var defaultFrom = DateTime.Today.AddDays(-30);
        var defaultTo = DateTime.Today;

        var rangeFromDate = (from ?? defaultFrom).Date;
        var rangeToDate = (to ?? defaultTo).Date;

        var rangeStart = rangeFromDate;
        var rangeEndExclusive = rangeToDate.AddDays(1);

        vm.TeamId = selectedTeamId;
        vm.From = rangeFromDate;
        vm.To = rangeToDate;

        var shifts = await _db.Shifts
            .Where(s => s.TeamId == selectedTeamId)
            .Where(s => s.End > rangeStart && s.Start < rangeEndExclusive)
            .Include(s => s.Assignments)
            .OrderBy(s => s.Start)
            .ToListAsync();

        var assignedUserIds = shifts
            .SelectMany(s => s.Assignments.Select(a => a.UserId))
            .Distinct()
            .ToList();

        var userMap = await _db.Users
            .Where(u => assignedUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.Email ?? u.UserName ?? "Użytkownik"));

        var hoursMap = new Dictionary<string, double>();

        foreach (var shift in shifts)
        {
            var start = shift.Start;
            var end = shift.End;

            var effectiveStart = start < rangeStart ? rangeStart : start;
            var effectiveEnd = end > rangeEndExclusive ? rangeEndExclusive : end;

            if (effectiveEnd <= effectiveStart)
                continue;

            var durationHours = (effectiveEnd - effectiveStart).TotalHours;

            foreach (var a in shift.Assignments)
            {
                if (!hoursMap.ContainsKey(a.UserId))
                    hoursMap[a.UserId] = 0;

                hoursMap[a.UserId] += durationHours;
            }
        }

        var rows = hoursMap
            .Select(kvp => new HoursReportViewModel.Row
            {
                Email = userMap.ContainsKey(kvp.Key) ? userMap[kvp.Key] : "Użytkownik",
                Hours = kvp.Value
            })
            .OrderBy(r => r.Email)
            .ToList();

        vm.Rows = rows;
        vm.TotalHours = rows.Sum(r => r.Hours);

        return View(vm);
    }
}
