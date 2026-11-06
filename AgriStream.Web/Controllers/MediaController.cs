using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriStream.Web.Controllers;

[Authorize]
public class MediaController : Controller
{
    private const long MaxBytes = 100L * 1024 * 1024; // 100 MB per file
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp", ".mp4", ".pdf", ".csv"
    };

    private readonly StorageService _storage;

    public MediaController(StorageService storage) => _storage = storage;

    public async Task<IActionResult> Index() => View(await _storage.ListMediaAsync());

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Farmer}")]
    [RequestSizeLimit(MaxBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBytes)]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please choose a file.";
            return RedirectToAction(nameof(Index));
        }

        var ext = Path.GetExtension(file.FileName);
        if (!Allowed.Contains(ext))
        {
            TempData["Error"] = $"File type '{ext}' is not allowed.";
            return RedirectToAction(nameof(Index));
        }

        var name = await _storage.UploadMediaAsync(file, User.Identity?.Name ?? "unknown");
        TempData["Message"] = $"Uploaded. The blob-triggered function will process '{name}' shortly.";
        return RedirectToAction(nameof(Index));
    }
}
