using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RosterHive.Data;
using RosterHive.Models;
using RosterHive.ViewModels;

namespace RosterHive.Controllers;

[Authorize]
public class NieobecnosciController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public NieobecnosciController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var requests = await _db.TimeOffRequests
            .Where(r => r.RequesterUserId == userId)
            .Include(r => r.Team)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var conflictMap = await BuildConflictMapAsync(requests);

        ViewBag.ConflictMap = conflictMap;
        ViewBag.Error = TempData["Error"] as string;
        ViewBag.Success = TempData["Success"] as string;

        return View(requests);
    }

    [HttpGet]
    public async Task<IActionResult> Utworz()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var teams = await _db.TeamMembers
            .Where(tm => tm.UserId == userId)
            .Select(tm => tm.Team)
            .OrderBy(t => t.Name)
            .ToListAsync();

        var vm = new TimeOffCreateViewModel
        {
            Teams = teams.Select(t => new TimeOffCreateViewModel.TeamOption
            {
                Id = t.Id,
                Name = t.Name
            }).ToList(),
            StartDate = DateTime.Today,
            EndDate = DateTime.Today
        };

        if (vm.Teams.Count > 0)
            vm.TeamId = vm.Teams[0].Id;

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Utworz(TimeOffCreateViewModel model)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var teams = await _db.TeamMembers
            .Where(tm => tm.UserId == userId)
            .Select(tm => tm.Team)
            .OrderBy(t => t.Name)
            .ToListAsync();

        model.Teams = teams.Select(t => new TimeOffCreateViewModel.TeamOption
        {
            Id = t.Id,
            Name = t.Name
        }).ToList();

        if (!ModelState.IsValid)
            return View(model);

        if (model.EndDate.Date < model.StartDate.Date)
        {
            ModelState.AddModelError(string.Empty, "Data zakończenia nie może być wcześniejsza niż data rozpoczęcia.");
            return View(model);
        }

        var isMember = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == model.TeamId && tm.UserId == userId);
        if (!isMember)
        {
            ModelState.AddModelError(string.Empty, "Nie należysz do wybranego zespołu.");
            return View(model);
        }

        var req = new TimeOffRequest
        {
            TeamId = model.TeamId,
            RequesterUserId = userId,
            Type = model.Type,
            StartDate = model.StartDate.Date,
            EndDate = model.EndDate.Date,
            Reason = string.IsNullOrWhiteSpace(model.Reason) ? null : model.Reason.Trim(),
            Status = TimeOffStatus.Oczekuje,
            CreatedAt = DateTime.UtcNow
        };

        _db.TimeOffRequests.Add(req);
        await _db.SaveChangesAsync();

        _db.TimeOffRequestEvents.Add(new TimeOffRequestEvent
        {
            TimeOffRequestId = req.Id,
            ActorUserId = userId,
            Action = TimeOffEventAction.Utworzono,
            Note = req.Reason,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = "Złożono wniosek o nieobecność.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anuluj(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var req = await _db.TimeOffRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (req == null)
            return NotFound();

        if (req.RequesterUserId != userId)
            return Forbid();

        if (req.Status != TimeOffStatus.Oczekuje)
        {
            TempData["Error"] = "Nie można anulować wniosku, który został już rozpatrzony.";
            return RedirectToAction(nameof(Index));
        }

        req.Status = TimeOffStatus.Anulowany;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewedByUserId = userId;
        req.ReviewNote = "Anulowano przez użytkownika.";

        _db.TimeOffRequestEvents.Add(new TimeOffRequestEvent
        {
            TimeOffRequestId = req.Id,
            ActorUserId = userId,
            Action = TimeOffEventAction.Anulowano,
            Note = null,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = "Anulowano wniosek.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Panel(int? teamId = null)
    {
        var teams = await _db.Teams.OrderBy(t => t.Name).ToListAsync();

        ViewBag.Teams = teams;
        ViewBag.SelectedTeamId = teamId;

        IQueryable<TimeOffRequest> query = _db.TimeOffRequests
            .Include(r => r.Team)
            .Where(r => r.Status == TimeOffStatus.Oczekuje);

        if (teamId.HasValue)
            query = query.Where(r => r.TeamId == teamId.Value);

        var requests = await query
            .OrderBy(r => r.StartDate)
            .ToListAsync();

        var conflictMap = await BuildConflictMapAsync(requests);
        ViewBag.ConflictMap = conflictMap;

        ViewBag.Error = TempData["Error"] as string;
        ViewBag.Success = TempData["Success"] as string;

        var userIds = requests.Select(r => r.RequesterUserId).Distinct().ToList();
        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.Email ?? u.UserName ?? "Użytkownik"));
        ViewBag.UserMap = userMap;

        return View(requests);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Zatwierdz(int id, string? note)
    {
        var adminId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(adminId))
            return Challenge();

        var req = await _db.TimeOffRequests.Include(r => r.Team).FirstOrDefaultAsync(r => r.Id == id);
        if (req == null)
            return NotFound();

        if (req.Status != TimeOffStatus.Oczekuje)
        {
            TempData["Error"] = "Ten wniosek został już rozpatrzony.";
            return RedirectToAction(nameof(Panel));
        }

        var conflicts = await HasShiftConflictsAsync(req);
        if (conflicts)
        {
            TempData["Error"] = "Nie można zatwierdzić wniosku: koliduje z istniejącymi zmianami pracownika w grafiku.";
            return RedirectToAction(nameof(Panel), new { teamId = req.TeamId });
        }

        req.Status = TimeOffStatus.Zatwierdzony;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewedByUserId = adminId;
        req.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        _db.TimeOffRequestEvents.Add(new TimeOffRequestEvent
        {
            TimeOffRequestId = req.Id,
            ActorUserId = adminId,
            Action = TimeOffEventAction.Zatwierdzono,
            Note = req.ReviewNote,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = "Zatwierdzono wniosek.";
        return RedirectToAction(nameof(Panel), new { teamId = req.TeamId });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Odrzuc(int id, string? note)
    {
        var adminId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(adminId))
            return Challenge();

        var req = await _db.TimeOffRequests.Include(r => r.Team).FirstOrDefaultAsync(r => r.Id == id);
        if (req == null)
            return NotFound();

        if (req.Status != TimeOffStatus.Oczekuje)
        {
            TempData["Error"] = "Ten wniosek został już rozpatrzony.";
            return RedirectToAction(nameof(Panel));
        }

        req.Status = TimeOffStatus.Odrzucony;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewedByUserId = adminId;
        req.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        _db.TimeOffRequestEvents.Add(new TimeOffRequestEvent
        {
            TimeOffRequestId = req.Id,
            ActorUserId = adminId,
            Action = TimeOffEventAction.Odrzucono,
            Note = req.ReviewNote,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = "Odrzucono wniosek.";
        return RedirectToAction(nameof(Panel), new { teamId = req.TeamId });
    }

    private async Task<Dictionary<int, bool>> BuildConflictMapAsync(List<TimeOffRequest> requests)
    {
        var map = new Dictionary<int, bool>();
        foreach (var r in requests)
            map[r.Id] = await HasShiftConflictsAsync(r);
        return map;
    }

    private async Task<bool> HasShiftConflictsAsync(TimeOffRequest req)
    {
        var start = req.StartDate.Date;
        var end = req.EndDate.Date;

        var rangeStart = start;
        var rangeEndExclusive = end.AddDays(1);

        var has = await _db.ShiftAssignments
            .Where(a => a.UserId == req.RequesterUserId)
            .Join(_db.Shifts, a => a.ShiftId, s => s.Id, (a, s) => new { s.TeamId, s.Start, s.End })
            .Where(x => x.TeamId == req.TeamId)
            .Where(x => x.End > rangeStart && x.Start < rangeEndExclusive)
            .AnyAsync();

        return has;
    }
}
