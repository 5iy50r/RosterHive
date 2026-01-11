using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RosterHive.Data;
using RosterHive.Models;
using RosterHive.ViewModels;

namespace RosterHive.Controllers;

[Authorize]
public class GrafikController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public GrafikController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Zespol(int teamId, DateTime? from = null, DateTime? to = null)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var isRootAdmin = User.IsInRole("Admin");

        var isMember = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId);
        if (!isMember && !isRootAdmin)
            return Forbid();

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null)
            return NotFound();

        var ownerMissing = string.IsNullOrWhiteSpace(team.OwnerUserId);
        var isOwner = !ownerMissing && team.OwnerUserId == userId;
        var canManage = isRootAdmin || isOwner;

        IQueryable<Shift> query = _db.Shifts
            .Where(s => s.TeamId == teamId)
            .Include(s => s.Assignments);

        if (from.HasValue)
            query = query.Where(s => s.Start >= from.Value);

        if (to.HasValue)
            query = query.Where(s => s.Start <= to.Value);

        var shifts = await query
            .OrderBy(s => s.Start)
            .ToListAsync();

        var userMap = await _db.Users.ToDictionaryAsync(u => u.Id, u => (u.Email ?? u.UserName ?? "Użytkownik"));

        string formatEmployees(Shift s)
        {
            var ids = s.Assignments.Select(a => a.UserId).Distinct().ToList();
            if (ids.Count == 0) return "Nieobsadzona";
            var labels = ids.Select(id => userMap.ContainsKey(id) ? userMap[id] : "Użytkownik").OrderBy(x => x).ToList();
            return string.Join(", ", labels);
        }

        var vm = new TeamShiftsViewModel
        {
            TeamId = team.Id,
            TeamName = team.Name,
            CanManage = canManage,
            From = from,
            To = to,
            Shifts = shifts.Select(s => new TeamShiftsViewModel.Row
            {
                Id = s.Id,
                Start = s.Start,
                End = s.End,
                EmployeesLabel = formatEmployees(s),
                Location = s.Location,
                Note = s.Note
            }).ToList()
        };

        ViewBag.Error = TempData["Error"] as string;
        ViewBag.Success = TempData["Success"] as string;

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Tydzien(int teamId, DateTime? day = null)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var isRootAdmin = User.IsInRole("Admin");

        var isMember = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId);
        if (!isMember && !isRootAdmin)
            return Forbid();

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null)
            return NotFound();

        var ownerMissing = string.IsNullOrWhiteSpace(team.OwnerUserId);
        var isOwner = !ownerMissing && team.OwnerUserId == userId;
        var canManage = isRootAdmin || isOwner;

        var baseDay = (day ?? DateTime.Today).Date;

        int delta = (int)baseDay.DayOfWeek - (int)DayOfWeek.Monday;
        if (delta < 0) delta += 7;
        var monday = baseDay.AddDays(-delta);

        var rangeStart = monday;
        var rangeEndExclusive = monday.AddDays(7);

        var shifts = await _db.Shifts
            .Where(s => s.TeamId == teamId)
            .Where(s => s.End > rangeStart && s.Start < rangeEndExclusive)
            .Include(s => s.Assignments)
            .OrderBy(s => s.Start)
            .ToListAsync();

        var userMap = await _db.Users.ToDictionaryAsync(u => u.Id, u => (u.Email ?? u.UserName ?? "Użytkownik"));

        string formatEmployees(Shift s)
        {
            var ids = s.Assignments.Select(a => a.UserId).Distinct().ToList();
            if (ids.Count == 0) return "Nieobsadzona";
            var labels = ids.Select(id => userMap.ContainsKey(id) ? userMap[id] : "Użytkownik").OrderBy(x => x).ToList();
            return string.Join(", ", labels);
        }

        var vm = new TeamShiftsViewModel
        {
            TeamId = team.Id,
            TeamName = team.Name,
            CanManage = canManage,
            From = monday,
            To = monday.AddDays(6),
            Shifts = shifts.Select(s => new TeamShiftsViewModel.Row
            {
                Id = s.Id,
                Start = s.Start,
                End = s.End,
                EmployeesLabel = formatEmployees(s),
                Location = s.Location,
                Note = s.Note
            }).ToList()
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Dodaj(int teamId)
    {
        var (allowed, _) = await CanManageTeamAsync(teamId);
        if (!allowed)
            return Forbid();

        var vm = new ShiftFormViewModel
        {
            TeamId = teamId,
            Start = DateTime.Now,
            End = DateTime.Now.AddHours(8),
            Employees = await GetTeamEmployeesAsync(teamId)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dodaj(ShiftFormViewModel model)
    {
        var (allowed, _) = await CanManageTeamAsync(model.TeamId);
        if (!allowed)
            return Forbid();

        if (!ModelState.IsValid)
        {
            model.Employees = await GetTeamEmployeesAsync(model.TeamId);
            return View(model);
        }

        if (model.End <= model.Start)
        {
            ModelState.AddModelError(string.Empty, "Zakończenie musi być później niż rozpoczęcie.");
            model.Employees = await GetTeamEmployeesAsync(model.TeamId);
            return View(model);
        }

        var validUserIds = await GetTeamUserIdsAsync(model.TeamId);
        var selected = model.SelectedUserIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (selected.Any(id => !validUserIds.Contains(id)))
        {
            ModelState.AddModelError(string.Empty, "Wybrano użytkownika, który nie należy do tego zespołu.");
            model.Employees = await GetTeamEmployeesAsync(model.TeamId);
            return View(model);
        }

        var conflicts = await FindConflictingUsersAsync(model.Start, model.End, selected, ignoreShiftId: null);
        if (conflicts.Count > 0)
        {
            var labels = await GetUserLabelsAsync(conflicts);
            ModelState.AddModelError(string.Empty, "Konflikt grafiku dla: " + string.Join(", ", labels) + ".");
            model.Employees = await GetTeamEmployeesAsync(model.TeamId);
            return View(model);
        }

        var shift = new Shift
        {
            TeamId = model.TeamId,
            Start = model.Start,
            End = model.End,
            Location = string.IsNullOrWhiteSpace(model.Location) ? null : model.Location.Trim(),
            Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim()
        };

        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync();

        foreach (var uid in selected)
        {
            _db.ShiftAssignments.Add(new ShiftAssignment
            {
                ShiftId = shift.Id,
                UserId = uid
            });
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = "Dodano zmianę.";
        return RedirectToAction(nameof(Zespol), new { teamId = model.TeamId });
    }

    [HttpGet]
    public async Task<IActionResult> Edytuj(int id)
    {
        var shift = await _db.Shifts
            .Include(s => s.Assignments)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (shift == null)
            return NotFound();

        var (allowed, _) = await CanManageTeamAsync(shift.TeamId);
        if (!allowed)
            return Forbid();

        var vm = new ShiftFormViewModel
        {
            Id = shift.Id,
            TeamId = shift.TeamId,
            Start = shift.Start,
            End = shift.End,
            Location = shift.Location,
            Note = shift.Note,
            SelectedUserIds = shift.Assignments.Select(a => a.UserId).Distinct().ToList(),
            Employees = await GetTeamEmployeesAsync(shift.TeamId)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edytuj(ShiftFormViewModel model)
    {
        if (!model.Id.HasValue)
            return NotFound();

        var shift = await _db.Shifts
            .Include(s => s.Assignments)
            .FirstOrDefaultAsync(s => s.Id == model.Id.Value);

        if (shift == null)
            return NotFound();

        var (allowed, _) = await CanManageTeamAsync(shift.TeamId);
        if (!allowed)
            return Forbid();

        if (!ModelState.IsValid)
        {
            model.Employees = await GetTeamEmployeesAsync(model.TeamId);
            return View(model);
        }

        if (model.End <= model.Start)
        {
            ModelState.AddModelError(string.Empty, "Zakończenie musi być później niż rozpoczęcie.");
            model.Employees = await GetTeamEmployeesAsync(model.TeamId);
            return View(model);
        }

        var validUserIds = await GetTeamUserIdsAsync(model.TeamId);
        var selected = model.SelectedUserIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (selected.Any(id => !validUserIds.Contains(id)))
        {
            ModelState.AddModelError(string.Empty, "Wybrano użytkownika, który nie należy do tego zespołu.");
            model.Employees = await GetTeamEmployeesAsync(model.TeamId);
            return View(model);
        }

        var conflicts = await FindConflictingUsersAsync(model.Start, model.End, selected, ignoreShiftId: shift.Id);
        if (conflicts.Count > 0)
        {
            var labels = await GetUserLabelsAsync(conflicts);
            ModelState.AddModelError(string.Empty, "Konflikt grafiku dla: " + string.Join(", ", labels) + ".");
            model.Employees = await GetTeamEmployeesAsync(model.TeamId);
            return View(model);
        }

        shift.Start = model.Start;
        shift.End = model.End;
        shift.Location = string.IsNullOrWhiteSpace(model.Location) ? null : model.Location.Trim();
        shift.Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();

        var current = shift.Assignments.Select(a => a.UserId).Distinct().ToHashSet();
        var target = selected.ToHashSet();

        var toRemove = shift.Assignments.Where(a => !target.Contains(a.UserId)).ToList();
        if (toRemove.Count > 0)
            _db.ShiftAssignments.RemoveRange(toRemove);

        foreach (var uid in target)
        {
            if (!current.Contains(uid))
            {
                _db.ShiftAssignments.Add(new ShiftAssignment
                {
                    ShiftId = shift.Id,
                    UserId = uid
                });
            }
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = "Zapisano zmiany.";
        return RedirectToAction(nameof(Zespol), new { teamId = shift.TeamId });
    }

    [HttpGet]
    public async Task<IActionResult> Usun(int id)
    {
        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == id);
        if (shift == null)
            return NotFound();

        var (allowed, team) = await CanManageTeamAsync(shift.TeamId);
        if (!allowed)
            return Forbid();

        ViewBag.TeamName = team?.Name ?? "Zespół";
        return View(shift);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UsunPotwierdz(int id)
    {
        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == id);
        if (shift == null)
            return NotFound();

        var (allowed, _) = await CanManageTeamAsync(shift.TeamId);
        if (!allowed)
            return Forbid();

        var teamId = shift.TeamId;

        _db.Shifts.Remove(shift);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Usunięto zmianę.";
        return RedirectToAction(nameof(Zespol), new { teamId });
    }

    private async Task<(bool allowed, Team? team)> CanManageTeamAsync(int teamId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return (false, null);

        var isRootAdmin = User.IsInRole("Admin");

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null)
            return (false, null);

        var ownerMissing = string.IsNullOrWhiteSpace(team.OwnerUserId);
        var isOwner = !ownerMissing && team.OwnerUserId == userId;

        if (isRootAdmin || isOwner)
            return (true, team);

        return (false, team);
    }

    private async Task<HashSet<string>> GetTeamUserIdsAsync(int teamId)
    {
        var ids = await _db.TeamMembers
            .Where(tm => tm.TeamId == teamId)
            .Select(tm => tm.UserId)
            .Distinct()
            .ToListAsync();

        return ids.ToHashSet();
    }

    private async Task<List<ShiftFormViewModel.EmployeeOption>> GetTeamEmployeesAsync(int teamId)
    {
        var userIds = await _db.TeamMembers
            .Where(tm => tm.TeamId == teamId)
            .Select(tm => tm.UserId)
            .Distinct()
            .ToListAsync();

        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .OrderBy(u => u.Email)
            .ToListAsync();

        return users.Select(u => new ShiftFormViewModel.EmployeeOption
        {
            UserId = u.Id,
            Label = u.Email ?? u.UserName ?? "Użytkownik"
        }).ToList();
    }

    private async Task<List<string>> FindConflictingUsersAsync(DateTime start, DateTime end, List<string> userIds, int? ignoreShiftId)
    {
        if (userIds.Count == 0)
            return new List<string>();

        var query = _db.ShiftAssignments
            .Where(a => userIds.Contains(a.UserId))
            .Join(_db.Shifts, a => a.ShiftId, s => s.Id, (a, s) => new { a.UserId, ShiftId = s.Id, s.Start, s.End });

        if (ignoreShiftId.HasValue)
            query = query.Where(x => x.ShiftId != ignoreShiftId.Value);

        query = query.Where(x => start < x.End && end > x.Start);

        var conflicts = await query
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync();

        return conflicts;
    }

    private async Task<List<string>> GetUserLabelsAsync(List<string> userIds)
    {
        if (userIds.Count == 0)
            return new List<string>();

        var map = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.Email ?? u.UserName ?? "Użytkownik"));

        var labels = userIds
            .Where(id => map.ContainsKey(id))
            .Select(id => map[id])
            .OrderBy(x => x)
            .ToList();

        return labels;
    }
}
