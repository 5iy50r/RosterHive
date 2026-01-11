using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RosterHive.Data;
using RosterHive.Models;
using RosterHive.ViewModels;

namespace RosterHive.Controllers;

[Authorize]
public class PodmianyController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public PodmianyController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
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

        var requests = await _db.ShiftSwapRequests
            .Where(r => r.RequesterUserId == userId)
            .Include(r => r.Team)
            .Include(r => r.Shift)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var userIds = requests
            .SelectMany(r => new[] { r.RequesterUserId, r.RequestedToUserId, r.TakenByUserId, r.ReviewedByUserId })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList()!;

        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.Email ?? u.UserName ?? "Użytkownik"));

        ViewBag.UserMap = userMap;
        ViewBag.Error = TempData["Error"] as string;
        ViewBag.Success = TempData["Success"] as string;

        return View(requests);
    }

    [HttpGet]
    public async Task<IActionResult> Utworz(int? teamId = null)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var teams = await _db.TeamMembers
            .Where(tm => tm.UserId == userId)
            .Select(tm => tm.Team)
            .OrderBy(t => t.Name)
            .ToListAsync();

        var vm = new ShiftSwapCreateViewModel
        {
            Teams = teams.Select(t => new ShiftSwapCreateViewModel.TeamOption
            {
                Id = t.Id,
                Name = t.Name
            }).ToList()
        };

        if (vm.Teams.Count == 0)
            return View(vm);

        vm.TeamId = teamId ?? vm.Teams[0].Id;

        await FillShiftAndUserOptionsAsync(vm, userId);

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Utworz(ShiftSwapCreateViewModel model)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var teams = await _db.TeamMembers
            .Where(tm => tm.UserId == userId)
            .Select(tm => tm.Team)
            .OrderBy(t => t.Name)
            .ToListAsync();

        model.Teams = teams.Select(t => new ShiftSwapCreateViewModel.TeamOption
        {
            Id = t.Id,
            Name = t.Name
        }).ToList();

        if (model.Teams.Count == 0)
            return View(model);

        await FillShiftAndUserOptionsAsync(model, userId);

        if (!ModelState.IsValid)
            return View(model);

        var isMember = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == model.TeamId && tm.UserId == userId);
        if (!isMember)
        {
            ModelState.AddModelError(string.Empty, "Nie należysz do wybranego zespołu.");
            return View(model);
        }

        var shift = await _db.Shifts
            .Include(s => s.Assignments)
            .FirstOrDefaultAsync(s => s.Id == model.ShiftId && s.TeamId == model.TeamId);

        if (shift == null)
        {
            ModelState.AddModelError(string.Empty, "Nie znaleziono wybranej zmiany.");
            return View(model);
        }

        var requesterAssigned = shift.Assignments.Any(a => a.UserId == userId);
        if (!requesterAssigned)
        {
            ModelState.AddModelError(string.Empty, "Nie możesz podmienić zmiany, do której nie jesteś przypisany.");
            return View(model);
        }

        if (shift.Start < DateTime.Now.AddMinutes(-1))
        {
            ModelState.AddModelError(string.Empty, "Nie można składać podmiany dla zmiany, która już się rozpoczęła.");
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.RequestedToUserId))
        {
            var targetIsMember = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == model.TeamId && tm.UserId == model.RequestedToUserId);
            if (!targetIsMember)
            {
                ModelState.AddModelError(string.Empty, "Wybrany użytkownik nie należy do tego zespołu.");
                return View(model);
            }

            if (model.RequestedToUserId == userId)
            {
                ModelState.AddModelError(string.Empty, "Nie możesz wskazać siebie jako adresata podmiany.");
                return View(model);
            }
        }

        var duplicate = await _db.ShiftSwapRequests.AnyAsync(r =>
            r.TeamId == model.TeamId &&
            r.ShiftId == model.ShiftId &&
            r.RequesterUserId == userId &&
            (r.Status == ShiftSwapStatus.Oczekuje || r.Status == ShiftSwapStatus.Przyjete));

        if (duplicate)
        {
            ModelState.AddModelError(string.Empty, "Masz już aktywną prośbę o podmianę dla tej zmiany.");
            return View(model);
        }

        var req = new ShiftSwapRequest
        {
            TeamId = model.TeamId,
            ShiftId = model.ShiftId,
            RequesterUserId = userId,
            RequestedToUserId = string.IsNullOrWhiteSpace(model.RequestedToUserId) ? null : model.RequestedToUserId,
            Status = ShiftSwapStatus.Oczekuje,
            Message = string.IsNullOrWhiteSpace(model.Message) ? null : model.Message.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.ShiftSwapRequests.Add(req);
        await _db.SaveChangesAsync();

        _db.ShiftSwapRequestEvents.Add(new ShiftSwapRequestEvent
        {
            ShiftSwapRequestId = req.Id,
            ActorUserId = userId,
            Action = ShiftSwapEventAction.Utworzono,
            Note = req.Message,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = "Utworzono prośbę o podmianę.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Dostepne(int? teamId = null)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var isRootAdmin = User.IsInRole("Admin");

        var myTeams = await _db.TeamMembers
            .Where(tm => tm.UserId == userId)
            .Select(tm => tm.Team)
            .OrderBy(t => t.Name)
            .ToListAsync();

        ViewBag.Teams = myTeams;
        ViewBag.SelectedTeamId = teamId;

        IQueryable<ShiftSwapRequest> query = _db.ShiftSwapRequests
            .Include(r => r.Team)
            .Include(r => r.Shift)
            .Where(r => r.Status == ShiftSwapStatus.Oczekuje)
            .Where(r => r.RequesterUserId != userId);

        if (!isRootAdmin)
        {
            var teamIds = myTeams.Select(t => t.Id).ToList();
            query = query.Where(r => teamIds.Contains(r.TeamId));
        }

        if (teamId.HasValue)
            query = query.Where(r => r.TeamId == teamId.Value);

        var requests = await query
            .OrderBy(r => r.Shift.Start)
            .ToListAsync();

        var userIds = requests
            .SelectMany(r => new[] { r.RequesterUserId, r.RequestedToUserId })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList()!;

        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.Email ?? u.UserName ?? "Użytkownik"));

        ViewBag.UserMap = userMap;
        ViewBag.Error = TempData["Error"] as string;
        ViewBag.Success = TempData["Success"] as string;

        var conflictMap = new Dictionary<int, bool>();
        foreach (var r in requests)
            conflictMap[r.Id] = await HasShiftConflictAsync(r.TeamId, r.ShiftId, userId);

        ViewBag.ConflictMap = conflictMap;

        return View(requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Przejmij(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var req = await _db.ShiftSwapRequests
            .Include(r => r.Shift)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (req == null)
            return NotFound();

        if (req.Status != ShiftSwapStatus.Oczekuje)
        {
            TempData["Error"] = "Ta prośba nie jest już dostępna.";
            return RedirectToAction(nameof(Dostepne), new { teamId = req.TeamId });
        }

        if (req.RequesterUserId == userId)
        {
            TempData["Error"] = "Nie możesz przejąć własnej podmiany.";
            return RedirectToAction(nameof(Dostepne), new { teamId = req.TeamId });
        }

        var isMember = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == req.TeamId && tm.UserId == userId);
        if (!isMember && !User.IsInRole("Admin"))
            return Forbid();

        if (!string.IsNullOrWhiteSpace(req.RequestedToUserId) && req.RequestedToUserId != userId)
        {
            TempData["Error"] = "Ta podmiana jest skierowana do konkretnej osoby.";
            return RedirectToAction(nameof(Dostepne), new { teamId = req.TeamId });
        }

        var conflict = await HasShiftConflictAsync(req.TeamId, req.ShiftId, userId);
        if (conflict)
        {
            TempData["Error"] = "Nie możesz przejąć tej podmiany: masz konflikt z własnym grafikiem.";
            return RedirectToAction(nameof(Dostepne), new { teamId = req.TeamId });
        }

        req.Status = ShiftSwapStatus.Przyjete;
        req.TakenByUserId = userId;
        req.TakenAt = DateTime.UtcNow;

        _db.ShiftSwapRequestEvents.Add(new ShiftSwapRequestEvent
        {
            ShiftSwapRequestId = req.Id,
            ActorUserId = userId,
            Action = ShiftSwapEventAction.Przyjeto,
            Note = null,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = "Przejęto podmianę. Czeka na zatwierdzenie przez administratora.";
        return RedirectToAction(nameof(Dostepne), new { teamId = req.TeamId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anuluj(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var req = await _db.ShiftSwapRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (req == null)
            return NotFound();

        if (req.RequesterUserId != userId)
            return Forbid();

        if (req.Status != ShiftSwapStatus.Oczekuje)
        {
            TempData["Error"] = "Nie można anulować podmiany, która została już przejęta lub rozpatrzona.";
            return RedirectToAction(nameof(Index));
        }

        req.Status = ShiftSwapStatus.Anulowane;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewedByUserId = userId;
        req.ReviewNote = "Anulowano przez użytkownika.";

        _db.ShiftSwapRequestEvents.Add(new ShiftSwapRequestEvent
        {
            ShiftSwapRequestId = req.Id,
            ActorUserId = userId,
            Action = ShiftSwapEventAction.Anulowano,
            Note = null,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = "Anulowano podmianę.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Panel(int? teamId = null)
    {
        var teams = await _db.Teams.OrderBy(t => t.Name).ToListAsync();
        ViewBag.Teams = teams;
        ViewBag.SelectedTeamId = teamId;

        IQueryable<ShiftSwapRequest> query = _db.ShiftSwapRequests
            .Include(r => r.Team)
            .Include(r => r.Shift)
            .Where(r => r.Status == ShiftSwapStatus.Przyjete);

        if (teamId.HasValue)
            query = query.Where(r => r.TeamId == teamId.Value);

        var requests = await query
            .OrderBy(r => r.Shift.Start)
            .ToListAsync();

        var userIds = requests
            .SelectMany(r => new[] { r.RequesterUserId, r.RequestedToUserId, r.TakenByUserId })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList()!;

        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.Email ?? u.UserName ?? "Użytkownik"));

        ViewBag.UserMap = userMap;
        ViewBag.Error = TempData["Error"] as string;
        ViewBag.Success = TempData["Success"] as string;

        var conflictMap = new Dictionary<int, bool>();
        foreach (var r in requests)
        {
            if (string.IsNullOrWhiteSpace(r.TakenByUserId))
                conflictMap[r.Id] = true;
            else
                conflictMap[r.Id] = await HasShiftConflictAsync(r.TeamId, r.ShiftId, r.TakenByUserId);
        }

        ViewBag.ConflictMap = conflictMap;

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

        var req = await _db.ShiftSwapRequests
            .Include(r => r.Shift)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (req == null)
            return NotFound();

        if (req.Status != ShiftSwapStatus.Przyjete || string.IsNullOrWhiteSpace(req.TakenByUserId))
        {
            TempData["Error"] = "Ta podmiana nie jest gotowa do zatwierdzenia.";
            return RedirectToAction(nameof(Panel), new { teamId = req.TeamId });
        }

        var shift = await _db.Shifts
            .Include(s => s.Assignments)
            .FirstOrDefaultAsync(s => s.Id == req.ShiftId);

        if (shift == null)
        {
            TempData["Error"] = "Nie znaleziono zmiany powiązanej z podmianą.";
            return RedirectToAction(nameof(Panel), new { teamId = req.TeamId });
        }

        var requesterStillAssigned = shift.Assignments.Any(a => a.UserId == req.RequesterUserId);
        if (!requesterStillAssigned)
        {
            TempData["Error"] = "Nie można zatwierdzić: pracownik nie jest już przypisany do tej zmiany.";
            return RedirectToAction(nameof(Panel), new { teamId = req.TeamId });
        }

        var takerIsMember = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == req.TeamId && tm.UserId == req.TakenByUserId);
        if (!takerIsMember)
        {
            TempData["Error"] = "Nie można zatwierdzić: przejmujący nie należy już do zespołu.";
            return RedirectToAction(nameof(Panel), new { teamId = req.TeamId });
        }

        var conflict = await HasShiftConflictAsync(req.TeamId, req.ShiftId, req.TakenByUserId);
        if (conflict)
        {
            TempData["Error"] = "Nie można zatwierdzić: przejmujący ma konflikt z grafikiem.";
            return RedirectToAction(nameof(Panel), new { teamId = req.TeamId });
        }

        var reqAssignment = shift.Assignments.FirstOrDefault(a => a.UserId == req.RequesterUserId);
        if (reqAssignment != null)
            _db.ShiftAssignments.Remove(reqAssignment);

        var already = shift.Assignments.Any(a => a.UserId == req.TakenByUserId);
        if (!already)
        {
            _db.ShiftAssignments.Add(new ShiftAssignment
            {
                ShiftId = shift.Id,
                UserId = req.TakenByUserId
            });
        }

        req.Status = ShiftSwapStatus.Zatwierdzone;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewedByUserId = adminId;
        req.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        _db.ShiftSwapRequestEvents.Add(new ShiftSwapRequestEvent
        {
            ShiftSwapRequestId = req.Id,
            ActorUserId = adminId,
            Action = ShiftSwapEventAction.Zatwierdzono,
            Note = req.ReviewNote,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = "Zatwierdzono podmianę i zaktualizowano obsadę zmiany.";
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

        var req = await _db.ShiftSwapRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (req == null)
            return NotFound();

        if (req.Status != ShiftSwapStatus.Przyjete)
        {
            TempData["Error"] = "Ta podmiana nie jest już gotowa do odrzucenia.";
            return RedirectToAction(nameof(Panel), new { teamId = req.TeamId });
        }

        req.Status = ShiftSwapStatus.Odrzucone;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewedByUserId = adminId;
        req.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        _db.ShiftSwapRequestEvents.Add(new ShiftSwapRequestEvent
        {
            ShiftSwapRequestId = req.Id,
            ActorUserId = adminId,
            Action = ShiftSwapEventAction.Odrzucono,
            Note = req.ReviewNote,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = "Odrzucono podmianę.";
        return RedirectToAction(nameof(Panel), new { teamId = req.TeamId });
    }

    private async Task FillShiftAndUserOptionsAsync(ShiftSwapCreateViewModel vm, string requesterUserId)
    {
        var shifts = await _db.ShiftAssignments
            .Where(a => a.UserId == requesterUserId)
            .Join(_db.Shifts, a => a.ShiftId, s => s.Id, (a, s) => s)
            .Where(s => s.TeamId == vm.TeamId)
            .Where(s => s.Start >= DateTime.Today)
            .OrderBy(s => s.Start)
            .ToListAsync();

        vm.Shifts = shifts.Select(s =>
        {
            var loc = string.IsNullOrWhiteSpace(s.Location) ? "" : $" • {s.Location}";
            return new ShiftSwapCreateViewModel.ShiftOption
            {
                Id = s.Id,
                Label = $"{s.Start:yyyy-MM-dd HH:mm} – {s.End:HH:mm}{loc}"
            };
        }).ToList();

        if (vm.Shifts.Count > 0 && vm.ShiftId == 0)
            vm.ShiftId = vm.Shifts[0].Id;

        var memberIds = await _db.TeamMembers
            .Where(tm => tm.TeamId == vm.TeamId && tm.UserId != requesterUserId)
            .Select(tm => tm.UserId)
            .Distinct()
            .ToListAsync();

        var users = await _db.Users
            .Where(u => memberIds.Contains(u.Id))
            .OrderBy(u => u.Email)
            .ToListAsync();

        vm.Users = users.Select(u => new ShiftSwapCreateViewModel.UserOption
        {
            UserId = u.Id,
            Label = u.Email ?? u.UserName ?? "Użytkownik"
        }).ToList();
    }

    private async Task<bool> HasShiftConflictAsync(int teamId, int shiftId, string userId)
    {
        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId && s.TeamId == teamId);
        if (shift == null)
            return true;

        var start = shift.Start;
        var end = shift.End;

        var has = await _db.ShiftAssignments
            .Where(a => a.UserId == userId)
            .Join(_db.Shifts, a => a.ShiftId, s => s.Id, (a, s) => new { s.TeamId, ShiftId = s.Id, s.Start, s.End })
            .Where(x => x.TeamId == teamId)
            .Where(x => x.ShiftId != shiftId)
            .Where(x => start < x.End && end > x.Start)
            .AnyAsync();

        return has;
    }
}
