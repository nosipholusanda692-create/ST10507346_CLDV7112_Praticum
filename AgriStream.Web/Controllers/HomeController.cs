using System.Diagnostics;
using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriStream.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly StorageService _storage;

    public HomeController(StorageService storage) => _storage = storage;

    public async Task<IActionResult> Index()
    {
        var vm = new TelemetryPageVm
        {
            FarmId = "farm-001",
            Readings = await _storage.GetLatestTelemetryAsync("farm-001", 10)
        };
        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Error()
    {
        ViewBag.RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }
}
