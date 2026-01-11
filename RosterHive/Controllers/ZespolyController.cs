using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RosterHive.Data;
using RosterHive.Models;
using RosterHive.ViewModels;
using System.Security.Cryptography;

namespace RosterHive.Controllers;

[Authorize]
public class ZespolyController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public ZespolyController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
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

        if (User.IsInRole("Admin"))
        {
            var allTeams = await _db.Teams
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View(allTeams);
        }

        var teams = await _db.TeamMembers
            .Where(tm => tm.UserId == userId)
            .Select(tm => tm.Team)
            .OrderBy(t => t.Name)
            .ToListAsync();

        return View(teams);
    }

    [HttpGet]
    public async Task<IActionResult> Szczegoly(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var isRootAdmin = User.IsInRole("Admin");

        var isMember = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == id && tm.UserId == userId);
        if (!isMember && !isRootAdmin)
            return Forbid();

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == id);
        if (team == null)
            return NotFound();

        var ownerMissing = string.IsNullOrWhiteSpace(team.OwnerUserId);
        var isOwner = !ownerMissing && team.OwnerUserId == userId;

        var canManage = isRootAdmin || isOwner;
        var canManageRoles = isRootAdmin || isOwner;

        var members = await _db.TeamMembers
            .Where(tm => tm.TeamId == id)
            .Join(_db.Users, tm => tm.UserId, u => u.Id, (tm, u) => new TeamDetailsViewModel.MemberRow
            {
                TeamMemberId = tm.Id,
                UserId = tm.UserId,
                Email = u.Email ?? u.UserName ?? "",
                JoinedAt = tm.JoinedAt,
                IsOwner = (!ownerMissing && tm.UserId == team.OwnerUserId)
            })
            .OrderBy(m => m.Email)
            .ToListAsync();

        var memberUserIds = members.Select(m => m.UserId).Distinct().ToHashSet();

        var availableUsers = new List<TeamDetailsViewModel.UserOption>();
        if (canManage)
        {
            availableUsers = await _db.Users
                .Where(u => !memberUserIds.Contains(u.Id))
                .OrderBy(u => u.Email)
                .Select(u => new TeamDetailsViewModel.UserOption
                {
                    UserId = u.Id,
                    Email = u.Email ?? u.UserName ?? ""
                })
                .ToListAsync();
        }

        var vm = new TeamDetailsViewModel
        {
            Id = team.Id,
            Name = team.Name,
            Description = team.Description,
            JoinCode = team.JoinCode,
            ShowJoinCode = isRootAdmin,
            CanManageMembers = canManage,
            OwnerMissing = ownerMissing,
            CanManageTeamRoles = canManageRoles,
            OwnerUserId = ownerMissing ? null : team.OwnerUserId,
            Members = members,
            AvailableUsers = availableUsers
        };

        ViewBag.Error = TempData["Error"] as string;
        ViewBag.Success = TempData["Success"] as string;

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DodajCzlonka(int teamId, string userIdToAdd)
    {
        var currentUserId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Challenge();

        var isRootAdmin = User.IsInRole("Admin");

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null)
            return NotFound();

        var ownerMissing = string.IsNullOrWhiteSpace(team.OwnerUserId);
        var isOwner = !ownerMissing && team.OwnerUserId == currentUserId;

        if (!isRootAdmin && !isOwner)
            return Forbid();

        if (string.IsNullOrWhiteSpace(userIdToAdd))
        {
            TempData["Error"] = "Wybierz użytkownika.";
            return RedirectToAction(nameof(Szczegoly), new { id = teamId });
        }

        var userExists = await _db.Users.AnyAsync(u => u.Id == userIdToAdd);
        if (!userExists)
        {
            TempData["Error"] = "Nie znaleziono użytkownika.";
            return RedirectToAction(nameof(Szczegoly), new { id = teamId });
        }

        var alreadyMember = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userIdToAdd);
        if (alreadyMember)
        {
            TempData["Error"] = "Ten użytkownik już należy do zespołu.";
            return RedirectToAction(nameof(Szczegoly), new { id = teamId });
        }

        _db.TeamMembers.Add(new TeamMember
        {
            TeamId = teamId,
            UserId = userIdToAdd,
            JoinedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = "Dodano użytkownika do zespołu.";
        return RedirectToAction(nameof(Szczegoly), new { id = teamId });
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Utworz()
    {
        return View(new TeamCreateViewModel());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Utworz(TeamCreateViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var joinCode = await GenerateUniqueJoinCodeAsync();

        var team = new Team
        {
            Name = model.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            JoinCode = joinCode,
            OwnerUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Teams.Add(team);
        await _db.SaveChangesAsync();

        _db.TeamMembers.Add(new TeamMember
        {
            TeamId = team.Id,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Szczegoly), new { id = team.Id });
    }

    [HttpGet]
    public IActionResult Dolacz()
    {
        return View(new JoinTeamViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dolacz(JoinTeamViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var code = model.JoinCode.Trim();

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.JoinCode == code);
        if (team == null)
        {
            ModelState.AddModelError(string.Empty, "Nie znaleziono zespołu o podanym kodzie.");
            return View(model);
        }

        var exists = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == team.Id && tm.UserId == userId);
        if (exists)
        {
            ModelState.AddModelError(string.Empty, "Już należysz do tego zespołu.");
            return View(model);
        }

        _db.TeamMembers.Add(new TeamMember
        {
            TeamId = team.Id,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Szczegoly), new { id = team.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UstawRoleWZespole(int teamId, string selectedUserId, string selectedTeamRole)
    {
        var currentUserId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Challenge();

        var isRootAdmin = User.IsInRole("Admin");

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null)
            return NotFound();

        var currentIsMember = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == teamId && tm.UserId == currentUserId);
        if (!currentIsMember && !isRootAdmin)
            return Forbid();

        var ownerMissing = string.IsNullOrWhiteSpace(team.OwnerUserId);
        var isOwner = !ownerMissing && team.OwnerUserId == currentUserId;

        if (!isRootAdmin && !isOwner)
            return Forbid();

        var targetIsMember = await _db.TeamMembers.AnyAsync(tm => tm.TeamId == teamId && tm.UserId == selectedUserId);
        if (!targetIsMember)
        {
            TempData["Error"] = "Wybrany użytkownik nie jest członkiem zespołu.";
            return RedirectToAction(nameof(Szczegoly), new { id = teamId });
        }

        if (selectedTeamRole == "Kierownik")
        {
            team.OwnerUserId = selectedUserId;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Zmieniono kierownika zespołu.";
            return RedirectToAction(nameof(Szczegoly), new { id = teamId });
        }

        if (selectedTeamRole == "Pracownik")
        {
            if (!string.IsNullOrWhiteSpace(team.OwnerUserId) && team.OwnerUserId == selectedUserId)
            {
                TempData["Error"] = "Nie można ustawić kierownika jako pracownika bez wskazania nowego kierownika. Wybierz innego użytkownika i ustaw go jako kierownika.";
                return RedirectToAction(nameof(Szczegoly), new { id = teamId });
            }

            TempData["Success"] = "Użytkownik jest pracownikiem w zespole.";
            return RedirectToAction(nameof(Szczegoly), new { id = teamId });
        }

        TempData["Error"] = "Nieprawidłowa rola zespołowa.";
        return RedirectToAction(nameof(Szczegoly), new { id = teamId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UsunCzlonka(int teamId, int teamMemberId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var isRootAdmin = User.IsInRole("Admin");

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null)
            return NotFound();

        var ownerMissing = string.IsNullOrWhiteSpace(team.OwnerUserId);
        var isOwner = !ownerMissing && team.OwnerUserId == userId;

        if (!isRootAdmin && !isOwner)
            return Forbid();

        var member = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.Id == teamMemberId && tm.TeamId == teamId);
        if (member == null)
            return NotFound();

        if (!ownerMissing && member.UserId == team.OwnerUserId)
        {
            TempData["Error"] = "Nie można usunąć kierownika zespołu. Najpierw ustaw innego kierownika.";
            return RedirectToAction(nameof(Szczegoly), new { id = teamId });
        }

        _db.TeamMembers.Remove(member);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Szczegoly), new { id = teamId });
    }

    [HttpGet]
    public async Task<IActionResult> Usun(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var isRootAdmin = User.IsInRole("Admin");

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == id);
        if (team == null)
            return NotFound();

        var ownerMissing = string.IsNullOrWhiteSpace(team.OwnerUserId);
        var isOwner = !ownerMissing && team.OwnerUserId == userId;

        if (!isRootAdmin && !isOwner)
            return Forbid();

        return View(team);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UsunPotwierdz(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var isRootAdmin = User.IsInRole("Admin");

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == id);
        if (team == null)
            return NotFound();

        var ownerMissing = string.IsNullOrWhiteSpace(team.OwnerUserId);
        var isOwner = !ownerMissing && team.OwnerUserId == userId;

        if (!isRootAdmin && !isOwner)
            return Forbid();

        _db.Teams.Remove(team);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task<string> GenerateUniqueJoinCodeAsync()
    {
        while (true)
        {
            var code = GenerateCode(8);
            var exists = await _db.Teams.AnyAsync(t => t.JoinCode == code);
            if (!exists)
                return code;
        }
    }

    private static string GenerateCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);

        var result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[bytes[i] % chars.Length];

        return new string(result);
    }
}
