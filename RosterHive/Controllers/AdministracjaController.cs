using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RosterHive.ViewModels;

namespace RosterHive.Controllers;

[Authorize(Roles = "Admin")]
public class AdministracjaController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IConfiguration _configuration;

    public AdministracjaController(UserManager<IdentityUser> userManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    private string RootEmail => _configuration["Security:RootAdminEmail"] ?? "admin@rosterhive.local";

    private async Task<bool> IsCurrentRootAsync()
    {
        var current = await _userManager.GetUserAsync(User);
        var email = current?.Email ?? current?.UserName ?? "";
        return string.Equals(email, RootEmail, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsRootUser(IdentityUser user)
    {
        var email = user.Email ?? user.UserName ?? "";
        return string.Equals(email, RootEmail, StringComparison.OrdinalIgnoreCase);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var currentUserId = _userManager.GetUserId(User) ?? "";
        var isCurrentRoot = await IsCurrentRootAsync();

        var users = await _userManager.Users
            .OrderBy(u => u.Email)
            .ToListAsync();

        var vm = new AdminUsersViewModel
        {
            IsCurrentRoot = isCurrentRoot,
            RootEmail = RootEmail
        };

        foreach (var u in users)
        {
            var isAdmin = await _userManager.IsInRoleAsync(u, "Admin");
            var isRoot = IsRootUser(u);

            var lockEnd = u.LockoutEnd;
            var locked = lockEnd.HasValue && lockEnd.Value.UtcDateTime > DateTime.UtcNow;

            var lockInfo = locked ? "Zablokowany" : "Aktywny";

            vm.Users.Add(new AdminUsersViewModel.UserRow
            {
                Id = u.Id,
                Email = u.Email ?? u.UserName ?? "(brak)",
                Role = isAdmin ? "Admin" : "User",
                IsAdmin = isAdmin,
                IsRoot = isRoot,
                IsLocked = locked,
                LockInfo = lockInfo,
                IsSelf = u.Id == currentUserId
            });
        }

        ViewBag.Error = TempData["Error"] as string;
        ViewBag.Success = TempData["Success"] as string;

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Zablokuj(string id)
    {
        var currentUserId = _userManager.GetUserId(User) ?? "";
        if (id == currentUserId)
        {
            TempData["Error"] = "Nie można zablokować aktualnie zalogowanego konta.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["Error"] = "Nie znaleziono użytkownika.";
            return RedirectToAction(nameof(Index));
        }

        if (IsRootUser(user))
        {
            TempData["Error"] = "Nie można zablokować konta root.";
            return RedirectToAction(nameof(Index));
        }

        var isCurrentRoot = await IsCurrentRootAsync();
        var isTargetAdmin = await _userManager.IsInRoleAsync(user, "Admin");

        if (!isCurrentRoot && isTargetAdmin)
        {
            TempData["Error"] = "Nie można blokować kont z rolą Admin.";
            return RedirectToAction(nameof(Index));
        }

        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        TempData["Success"] = "Użytkownik został zablokowany.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Odblokuj(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["Error"] = "Nie znaleziono użytkownika.";
            return RedirectToAction(nameof(Index));
        }

        if (IsRootUser(user))
        {
            TempData["Error"] = "Nie można odblokowywać konta root.";
            return RedirectToAction(nameof(Index));
        }

        var isCurrentRoot = await IsCurrentRootAsync();
        var isTargetAdmin = await _userManager.IsInRoleAsync(user, "Admin");

        if (!isCurrentRoot && isTargetAdmin)
        {
            TempData["Error"] = "Nie można odblokowywać kont z rolą Admin.";
            return RedirectToAction(nameof(Index));
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.SetLockoutEnabledAsync(user, true);

        TempData["Success"] = "Użytkownik został odblokowany.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NadajAdmina(string id)
    {
        var isCurrentRoot = await IsCurrentRootAsync();
        if (!isCurrentRoot)
        {
            TempData["Error"] = "Brak uprawnień do nadawania roli Admin.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["Error"] = "Nie znaleziono użytkownika.";
            return RedirectToAction(nameof(Index));
        }

        if (IsRootUser(user))
        {
            TempData["Error"] = "Konto root ma stałe uprawnienia.";
            return RedirectToAction(nameof(Index));
        }

        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            TempData["Error"] = "Użytkownik już posiada rolę Admin.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _userManager.AddToRoleAsync(user, "Admin");
        if (!result.Succeeded)
        {
            TempData["Error"] = "Nie udało się nadać roli Admin.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Nadano uprawnienia Admin.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OdbierzAdmina(string id)
    {
        var isCurrentRoot = await IsCurrentRootAsync();
        if (!isCurrentRoot)
        {
            TempData["Error"] = "Brak uprawnień do odbierania roli Admin.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["Error"] = "Nie znaleziono użytkownika.";
            return RedirectToAction(nameof(Index));
        }

        if (IsRootUser(user))
        {
            TempData["Error"] = "Nie można odebrać roli Admin kontu root.";
            return RedirectToAction(nameof(Index));
        }

        if (!await _userManager.IsInRoleAsync(user, "Admin"))
        {
            TempData["Error"] = "Użytkownik nie posiada roli Admin.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _userManager.RemoveFromRoleAsync(user, "Admin");
        if (!result.Succeeded)
        {
            TempData["Error"] = "Nie udało się odebrać roli Admin.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Odebrano uprawnienia Admin.";
        return RedirectToAction(nameof(Index));
    }
}
