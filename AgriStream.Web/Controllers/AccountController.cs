using System.Security.Claims;
using AgriStream.Web.Data;
using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;

    public AccountController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(vm);

        var user = await _db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == vm.Username);

        if (user == null || !PasswordHasher.Verify(vm.Password, user.PasswordHash))
        {
            ModelState.AddModelError("", "Invalid username or password.");
            return View(vm);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role!.Name)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        if (await _db.Users.AnyAsync(u => u.Username == vm.Username))
        {
            ModelState.AddModelError(nameof(vm.Username), "That username is already taken.");
            return View(vm);
        }

        var email = vm.Email.Trim();
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            ModelState.AddModelError(nameof(vm.Email), "That email address is already registered.");
            return View(vm);
        }

        // Self-registered users always start as Farmer. An Admin can change the role later.
        var role = await _db.Roles.FirstAsync(r => r.Name == RoleNames.Farmer);
        _db.Users.Add(new AppUser
        {
            Username = vm.Username.Trim(),
            Email = email,
            PasswordHash = PasswordHasher.Hash(vm.Password),
            RoleId = role.Id
        });
        await _db.SaveChangesAsync();

        TempData["Message"] = "Account created. You can now sign in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Denied() => View();
}
