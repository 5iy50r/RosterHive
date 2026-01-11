using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RosterHive.Data;
using RosterHive.Models;
using RosterHive.ViewModels;

namespace RosterHive.Controllers;

[Authorize]
public class ZadaniaController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public ZadaniaController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Moje(TaskItemStatus? status = null)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        IQueryable<TaskItem> query = _db.TaskItems
            .Include(t => t.Team)
            .Include(t => t.Shift)
            .Where(t => t.AssignedToUserId == userId);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        var list = await query
            .OrderBy(t => t.Status)
            .ThenBy(t => t.DueDate == null)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync();

        ViewBag.SelectedStatus = status;
        ViewBag.Error = TempData["Error"] as string;
        ViewBag.Success = TempData["Success"] as string;

        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> Zespol(int teamId, TaskItemStatus? status = null, string? assignedTo = null)
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

        var canManage = isRootAdmin || (!string.IsNullOrWhiteSpace(team.OwnerUserId) && team.OwnerUserId == userId);

        IQueryable<TaskItem> query = _db.TaskItems
            .Include(t => t.Team)
            .Include(t => t.Shift)
            .Where(t => t.TeamId == teamId);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(assignedTo))
            query = query.Where(t => t.AssignedToUserId == assignedTo);

        var list = await query
            .OrderBy(t => t.Status)
            .ThenBy(t => t.DueDate == null)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync();

        var memberIds = await _db.TeamMembers
            .Where(tm => tm.TeamId == teamId)
            .Select(tm => tm.UserId)
            .Distinct()
            .ToListAsync();

        var userMap = await _db.Users
            .Where(u => memberIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.Email ?? u.UserName ?? "Użytkownik"));

        ViewBag.Team = team;
        ViewBag.CanManage = canManage;
        ViewBag.UserMap = userMap;
        ViewBag.SelectedStatus = status;
        ViewBag.SelectedAssignedTo = assignedTo;
        ViewBag.Error = TempData["Error"] as string;
        ViewBag.Success = TempData["Success"] as string;

        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> Utworz(int teamId)
    {
        var (allowed, team, userId) = await CanManageTeamAsync(teamId);
        if (!allowed)
            return Forbid();

        var vm = new TaskFormViewModel
        {
            TeamId = teamId,
            Priority = TaskPriority.Sredni,
            Status = TaskItemStatus.Nowe,
            Teams = new List<TaskFormViewModel.TeamOption> { new() { Id = team!.Id, Name = team.Name } }
        };

        await FillOptionsForTeamAsync(vm);

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Utworz(TaskFormViewModel model)
    {
        var (allowed, team, userId) = await CanManageTeamAsync(model.TeamId);
        if (!allowed)
            return Forbid();

        model.Teams = new List<TaskFormViewModel.TeamOption> { new() { Id = team!.Id, Name = team.Name } };
        await FillOptionsForTeamAsync(model);

        if (!ModelState.IsValid)
            return View(model);

        if (model.DueDate.HasValue)
            model.DueDate = model.DueDate.Value.Date;

        if (!string.IsNullOrWhiteSpace(model.AssignedToUserId))
        {
            var ok = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == model.TeamId && tm.UserId == model.AssignedToUserId);
            if (!ok)
            {
                ModelState.AddModelError(string.Empty, "Wybrany użytkownik nie należy do tego zespołu.");
                return View(model);
            }
        }

        if (model.ShiftId.HasValue)
        {
            var ok = await _db.Shifts.AnyAsync(s => s.Id == model.ShiftId.Value && s.TeamId == model.TeamId);
            if (!ok)
            {
                ModelState.AddModelError(string.Empty, "Wybrana zmiana nie należy do tego zespołu.");
                return View(model);
            }
        }

        var task = new TaskItem
        {
            TeamId = model.TeamId,
            ShiftId = model.ShiftId,
            Title = model.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            Priority = model.Priority,
            Status = model.Status,
            DueDate = model.DueDate,
            AssignedToUserId = string.IsNullOrWhiteSpace(model.AssignedToUserId) ? null : model.AssignedToUserId,
            CreatedByUserId = userId!,
            CreatedAt = DateTime.UtcNow
        };

        if (task.Status == TaskItemStatus.Zakonczone)
            task.CompletedAt = DateTime.UtcNow;

        _db.TaskItems.Add(task);
        await _db.SaveChangesAsync();

        _db.TaskComments.Add(new TaskComment
        {
            TaskItemId = task.Id,
            AuthorUserId = userId!,
            Type = TaskActivityType.Utworzono,
            Content = null,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = "Utworzono zadanie.";
        return RedirectToAction(nameof(Zespol), new { teamId = model.TeamId });
    }

    [HttpGet]
    public async Task<IActionResult> Edytuj(int id)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
        if (task == null)
            return NotFound();

        var (allowed, team, _) = await CanManageTeamAsync(task.TeamId);
        if (!allowed)
            return Forbid();

        var vm = new TaskFormViewModel
        {
            Id = task.Id,
            TeamId = task.TeamId,
            ShiftId = task.ShiftId,
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            Status = task.Status,
            DueDate = task.DueDate,
            AssignedToUserId = task.AssignedToUserId,
            Teams = new List<TaskFormViewModel.TeamOption> { new() { Id = team!.Id, Name = team.Name } }
        };

        await FillOptionsForTeamAsync(vm);

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edytuj(TaskFormViewModel model)
    {
        if (!model.Id.HasValue)
            return NotFound();

        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == model.Id.Value);
        if (task == null)
            return NotFound();

        var (allowed, team, userId) = await CanManageTeamAsync(task.TeamId);
        if (!allowed)
            return Forbid();

        model.TeamId = task.TeamId;
        model.Teams = new List<TaskFormViewModel.TeamOption> { new() { Id = team!.Id, Name = team.Name } };
        await FillOptionsForTeamAsync(model);

        if (!ModelState.IsValid)
            return View(model);

        var oldAssigned = task.AssignedToUserId;
        var oldStatus = task.Status;
        var oldDue = task.DueDate;
        var oldPriority = task.Priority;
        var oldShiftId = task.ShiftId;

        var newAssigned = string.IsNullOrWhiteSpace(model.AssignedToUserId) ? null : model.AssignedToUserId;
        var newDue = model.DueDate?.Date;
        var newShiftId = model.ShiftId;

        if (!string.IsNullOrWhiteSpace(newAssigned))
        {
            var ok = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == task.TeamId && tm.UserId == newAssigned);
            if (!ok)
            {
                ModelState.AddModelError(string.Empty, "Wybrany użytkownik nie należy do tego zespołu.");
                return View(model);
            }
        }

        if (newShiftId.HasValue)
        {
            var ok = await _db.Shifts.AnyAsync(s => s.Id == newShiftId.Value && s.TeamId == task.TeamId);
            if (!ok)
            {
                ModelState.AddModelError(string.Empty, "Wybrana zmiana nie należy do tego zespołu.");
                return View(model);
            }
        }

        task.Title = model.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        task.Priority = model.Priority;
        task.Status = model.Status;
        task.DueDate = newDue;
        task.AssignedToUserId = newAssigned;
        task.ShiftId = newShiftId;
        task.UpdatedAt = DateTime.UtcNow;

        if (oldStatus != TaskItemStatus.Zakonczone && task.Status == TaskItemStatus.Zakonczone)
            task.CompletedAt = DateTime.UtcNow;

        if (oldStatus == TaskItemStatus.Zakonczone && task.Status != TaskItemStatus.Zakonczone)
            task.CompletedAt = null;

        var activities = new List<TaskComment>();

        if (oldAssigned != task.AssignedToUserId)
        {
            activities.Add(new TaskComment
            {
                TaskItemId = task.Id,
                AuthorUserId = userId!,
                Type = TaskActivityType.ZmienionoPrzypisanie,
                Content = "Zmieniono przypisanie.",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (oldStatus != task.Status)
        {
            activities.Add(new TaskComment
            {
                TaskItemId = task.Id,
                AuthorUserId = userId!,
                Type = TaskActivityType.ZmienionoStatus,
                Content = $"Status: {oldStatus} → {task.Status}",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (oldDue != task.DueDate)
        {
            activities.Add(new TaskComment
            {
                TaskItemId = task.Id,
                AuthorUserId = userId!,
                Type = TaskActivityType.ZmienionoTermin,
                Content = "Zmieniono termin.",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (oldPriority != task.Priority)
        {
            activities.Add(new TaskComment
            {
                TaskItemId = task.Id,
                AuthorUserId = userId!,
                Type = TaskActivityType.ZmienionoPriorytet,
                Content = "Zmieniono priorytet.",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (oldShiftId != task.ShiftId)
        {
            activities.Add(new TaskComment
            {
                TaskItemId = task.Id,
                AuthorUserId = userId!,
                Type = TaskActivityType.ZmienionoPowiazanieZmiany,
                Content = "Zmieniono powiązanie ze zmianą.",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (activities.Count > 0)
            _db.TaskComments.AddRange(activities);

        await _db.SaveChangesAsync();

        TempData["Success"] = "Zapisano zmiany.";
        return RedirectToAction(nameof(Szczegoly), new { id = task.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Szczegoly(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var task = await _db.TaskItems
            .Include(t => t.Team)
            .Include(t => t.Shift)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound();

        var isRootAdmin = User.IsInRole("Admin");
        var isMember = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == task.TeamId && tm.UserId == userId);
        if (!isMember && !isRootAdmin)
            return Forbid();

        var canManage = isRootAdmin || (!string.IsNullOrWhiteSpace(task.Team.OwnerUserId) && task.Team.OwnerUserId == userId);
        var canChangeStatus = canManage || (!string.IsNullOrWhiteSpace(task.AssignedToUserId) && task.AssignedToUserId == userId);

        var ids = new List<string> { task.CreatedByUserId };
        if (!string.IsNullOrWhiteSpace(task.AssignedToUserId)) ids.Add(task.AssignedToUserId);
        ids.AddRange(task.Comments.Select(c => c.AuthorUserId));
        ids = ids.Distinct().ToList();

        var userMap = await _db.Users
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.Email ?? u.UserName ?? "Użytkownik"));

        var vm = new TaskDetailsViewModel
        {
            Task = task,
            CanManage = canManage,
            CanChangeStatus = canChangeStatus,
            AssignedLabel = string.IsNullOrWhiteSpace(task.AssignedToUserId) ? null : (userMap.ContainsKey(task.AssignedToUserId) ? userMap[task.AssignedToUserId] : "Użytkownik"),
            CreatedByLabel = userMap.ContainsKey(task.CreatedByUserId) ? userMap[task.CreatedByUserId] : "Użytkownik",
            Activity = task.Comments
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new TaskDetailsViewModel.ActivityRow
                {
                    CreatedAt = c.CreatedAt,
                    AuthorLabel = userMap.ContainsKey(c.AuthorUserId) ? userMap[c.AuthorUserId] : "Użytkownik",
                    Type = c.Type,
                    Content = c.Content
                }).ToList()
        };

        ViewBag.Error = TempData["Error"] as string;
        ViewBag.Success = TempData["Success"] as string;

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ZmienStatus(int id, TaskItemStatus status)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var task = await _db.TaskItems.Include(t => t.Team).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null)
            return NotFound();

        var isRootAdmin = User.IsInRole("Admin");
        var isMember = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == task.TeamId && tm.UserId == userId);
        if (!isMember && !isRootAdmin)
            return Forbid();

        var canManage = isRootAdmin || (!string.IsNullOrWhiteSpace(task.Team.OwnerUserId) && task.Team.OwnerUserId == userId);
        var canChange = canManage || (!string.IsNullOrWhiteSpace(task.AssignedToUserId) && task.AssignedToUserId == userId);

        if (!canChange)
            return Forbid();

        var old = task.Status;
        if (old == status)
            return RedirectToAction(nameof(Szczegoly), new { id });

        task.Status = status;
        task.UpdatedAt = DateTime.UtcNow;

        if (old != TaskItemStatus.Zakonczone && status == TaskItemStatus.Zakonczone)
            task.CompletedAt = DateTime.UtcNow;

        if (old == TaskItemStatus.Zakonczone && status != TaskItemStatus.Zakonczone)
            task.CompletedAt = null;

        _db.TaskComments.Add(new TaskComment
        {
            TaskItemId = task.Id,
            AuthorUserId = userId,
            Type = TaskActivityType.ZmienionoStatus,
            Content = $"Status: {old} → {status}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = "Zmieniono status.";
        return RedirectToAction(nameof(Szczegoly), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DodajKomentarz(int id, string content)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
        if (task == null)
            return NotFound();

        var isRootAdmin = User.IsInRole("Admin");
        var isMember = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == task.TeamId && tm.UserId == userId);
        if (!isMember && !isRootAdmin)
            return Forbid();

        var text = (content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            TempData["Error"] = "Komentarz nie może być pusty.";
            return RedirectToAction(nameof(Szczegoly), new { id });
        }

        if (text.Length > 800)
        {
            TempData["Error"] = "Komentarz jest zbyt długi (maks. 800 znaków).";
            return RedirectToAction(nameof(Szczegoly), new { id });
        }

        _db.TaskComments.Add(new TaskComment
        {
            TaskItemId = task.Id,
            AuthorUserId = userId,
            Type = TaskActivityType.Komentarz,
            Content = text,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = "Dodano komentarz.";
        return RedirectToAction(nameof(Szczegoly), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Usun(int id)
    {
        var task = await _db.TaskItems.Include(t => t.Team).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null)
            return NotFound();

        var (allowed, _, _) = await CanManageTeamAsync(task.TeamId);
        if (!allowed)
            return Forbid();

        return View(task);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UsunPotwierdz(int id)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
        if (task == null)
            return NotFound();

        var (allowed, _, _) = await CanManageTeamAsync(task.TeamId);
        if (!allowed)
            return Forbid();

        var teamId = task.TeamId;

        _db.TaskItems.Remove(task);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Usunięto zadanie.";
        return RedirectToAction(nameof(Zespol), new { teamId });
    }

    private async Task<(bool allowed, Team? team, string? userId)> CanManageTeamAsync(int teamId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return (false, null, null);

        var isRootAdmin = User.IsInRole("Admin");

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null)
            return (false, null, userId);

        var isOwner = !string.IsNullOrWhiteSpace(team.OwnerUserId) && team.OwnerUserId == userId;

        if (isRootAdmin || isOwner)
            return (true, team, userId);

        return (false, team, userId);
    }

    private async Task FillOptionsForTeamAsync(TaskFormViewModel vm)
    {
        var memberIds = await _db.TeamMembers
            .Where(tm => tm.TeamId == vm.TeamId)
            .Select(tm => tm.UserId)
            .Distinct()
            .ToListAsync();

        var users = await _db.Users
            .Where(u => memberIds.Contains(u.Id))
            .OrderBy(u => u.Email)
            .ToListAsync();

        vm.Users = users.Select(u => new TaskFormViewModel.UserOption
        {
            UserId = u.Id,
            Label = u.Email ?? u.UserName ?? "Użytkownik"
        }).ToList();

        var shifts = await _db.Shifts
            .Where(s => s.TeamId == vm.TeamId)
            .OrderByDescending(s => s.Start)
            .Take(50)
            .ToListAsync();

        vm.Shifts = shifts.Select(s =>
        {
            var loc = string.IsNullOrWhiteSpace(s.Location) ? "" : $" • {s.Location}";
            return new TaskFormViewModel.ShiftOption
            {
                Id = s.Id,
                Label = $"{s.Start:yyyy-MM-dd HH:mm} – {s.End:HH:mm}{loc}"
            };
        }).ToList();
    }
}
