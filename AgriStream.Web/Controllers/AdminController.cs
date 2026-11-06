using System.Security.Claims;
using AgriStream.Web.Data;
using AgriStream.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

[Authorize(Roles = RoleNames.Admin)]
public class AdminController : Controller
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var vm = new AdminVm
        {
            Users = await _db.Users.Include(u => u.Role).OrderBy(u => u.Username).ToListAsync(),
            Roles = await _db.Roles.OrderBy(r => r.Name).ToListAsync()
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(int id, int roleId)
    {
        var user = await _db.Users.FindAsync(id);
        var role = await _db.Roles.FindAsync(roleId);
        if (user == null || role == null) return NotFound();

        var currentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentId == user.Id.ToString() && role.Name != RoleNames.Admin)
        {
            TempData["Error"] = "You cannot remove your own Admin role.";
            return RedirectToAction(nameof(Index));
        }

        user.RoleId = role.Id;
        await _db.SaveChangesAsync();
        TempData["Message"] = $"{user.Username} is now {role.Name}. (They must sign in again for it to apply.)";
        return RedirectToAction(nameof(Index));
    }
}
