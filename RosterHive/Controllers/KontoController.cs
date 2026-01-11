using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RosterHive.ViewModels;

namespace RosterHive.Controllers;

public class KontoController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;

    public KontoController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Logowanie(string? returnUrl = null)
    {
        ViewBag.Blocked = false;
        return View(new LoginViewModel { ReturnUrl = returnUrl ?? Url.Content("~/") });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logowanie(LoginViewModel model)
    {
        ViewBag.Blocked = false;

        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Nieprawidłowy e-mail lub hasło.");
            return View(model);
        }

        var isLocked = await _userManager.IsLockedOutAsync(user);
        if (isLocked)
        {
            ViewBag.Blocked = true;
            ModelState.AddModelError(string.Empty, "Zablokowano. Brak dostępu.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
            return LocalRedirect(model.ReturnUrl ?? Url.Content("~/"));

        if (result.IsLockedOut)
        {
            ViewBag.Blocked = true;
            ModelState.AddModelError(string.Empty, "Zablokowano. Brak dostępu.");
            return View(model);
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Logowanie jest niedozwolone dla tego konta.");
            return View(model);
        }

        if (result.RequiresTwoFactor)
        {
            ModelState.AddModelError(string.Empty, "Wymagane jest uwierzytelnienie dwuetapowe.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Nieprawidłowy e-mail lub hasło.");
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Rejestracja(string? returnUrl = null)
    {
        return View(new RegisterViewModel { ReturnUrl = returnUrl ?? Url.Content("~/") });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rejestracja(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = new IdentityUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            LockoutEnabled = true
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);

        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "User");
        if (!roleResult.Succeeded)
        {
            foreach (var error in roleResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            await _userManager.DeleteAsync(user);
            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(model.ReturnUrl ?? Url.Content("~/"));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Wyloguj(string? returnUrl = null)
    {
        await _signInManager.SignOutAsync();
        return LocalRedirect(returnUrl ?? Url.Content("~/"));
    }

    [HttpGet]
    public IActionResult BrakDostepu()
    {
        return View();
    }
}
