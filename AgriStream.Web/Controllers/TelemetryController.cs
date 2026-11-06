using System.Text;
using System.Text.Json;
using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriStream.Web.Controllers;

[Authorize]
public class TelemetryController : Controller
{
    private readonly StorageService _storage;
    private readonly IHttpClientFactory _http;

    public TelemetryController(StorageService storage, IHttpClientFactory http)
    {
        _storage = storage;
        _http = http;
    }

    public async Task<IActionResult> Index(string farmId = "farm-001")
    {
        farmId = string.IsNullOrWhiteSpace(farmId) ? "farm-001" : farmId.Trim();
        var vm = new TelemetryPageVm
        {
            FarmId = farmId,
            Readings = await _storage.GetLatestTelemetryAsync(farmId, 50)
        };
        return View(vm);
    }

    /// <summary>
    /// Sends simulated sensor readings to the HTTP-triggered POST function,
    /// which puts them on the queue (queue-triggered function then writes them to Table Storage).
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Technician}")]
    public async Task<IActionResult> Send(string farmId = "farm-001", int count = 10)
    {
        count = Math.Clamp(count, 1, 100);
        var client = _http.CreateClient("functions");
        if (client.BaseAddress == null)
        {
            TempData["Error"] = "Functions:BaseUrl / Functions:Key are not configured.";
            return RedirectToAction(nameof(Index), new { farmId });
        }

        var rnd = Random.Shared;
        var batch = Enumerable.Range(1, count).Select(i => new
        {
            farmId,
            sensorId = $"sensor-{rnd.Next(1, 50):D3}",
            eventType = "reading",
            soilMoisture = Math.Round(rnd.NextDouble() * 60 + 5, 1),
            soilTemperature = Math.Round(rnd.NextDouble() * 15 + 12, 1),
            ph = Math.Round(rnd.NextDouble() * 3 + 5, 1),
            readingTimeUtc = DateTimeOffset.UtcNow
        }).ToList();

        var json = JsonSerializer.Serialize(batch, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var resp = await client.PostAsync("telemetry", content);
            if (resp.IsSuccessStatusCode)
                TempData["Message"] = $"Sent {count} readings to the pipeline (HTTP {(int)resp.StatusCode}). Refresh in a few seconds.";
            else
                TempData["Error"] = $"Function returned HTTP {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Could not reach the function: " + ex.Message;
        }

        return RedirectToAction(nameof(Index), new { farmId });
    }
}
