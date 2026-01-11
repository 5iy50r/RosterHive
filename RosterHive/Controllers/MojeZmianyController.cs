using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RosterHive.Data;
using RosterHive.ViewModels;

namespace RosterHive.Controllers;

[Authorize]
public class MojeZmianyController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public MojeZmianyController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
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

        var rows = await _db.ShiftAssignments
            .Where(a => a.UserId == userId)
            .Join(_db.Shifts, a => a.ShiftId, s => s.Id, (a, s) => s)
            .Join(_db.Teams, s => s.TeamId, t => t.Id, (s, t) => new MyShiftsViewModel.Row
            {
                TeamName = t.Name,
                Start = s.Start,
                End = s.End,
                Note = s.Note
            })
            .OrderBy(x => x.Start)
            .ToListAsync();

        var vm = new MyShiftsViewModel { Shifts = rows };
        return View(vm);
    }
}
