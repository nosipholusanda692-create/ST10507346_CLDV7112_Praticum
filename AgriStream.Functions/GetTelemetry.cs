using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions;

/// <summary>
/// FUNCTION 2 - HTTP triggered (GET).
///   GET /api/telemetry/{farmId}?limit=50&amp;sensorId=sensor-001
/// Returns the newest telemetry rows for a farm straight from Table Storage.
/// </summary>
public class GetTelemetry
{
    private readonly TableStore _store;
    private readonly ILogger<GetTelemetry> _log;

    public GetTelemetry(TableStore store, ILogger<GetTelemetry> log)
    {
        _store = store;
        _log = log;
    }

    [Function("GetTelemetry")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "telemetry/{farmId}")] HttpRequest req,
        string farmId)
    {
        if (string.IsNullOrWhiteSpace(farmId))
            return new BadRequestObjectResult(new { error = "farmId is required" });

        var limit = int.TryParse(req.Query["limit"].ToString(), out var l) ? Math.Clamp(l, 1, 500) : 50;
        var sensorId = req.Query["sensorId"].ToString();

        var rows = new List<object>();
        await foreach (var e in _store.Telemetry.QueryAsync<TelemetryEntity>(x => x.PartitionKey == farmId, maxPerPage: limit))
        {
            if (!string.IsNullOrEmpty(sensorId) && e.SensorId != sensorId) continue;

            rows.Add(new
            {
                farmId = e.PartitionKey,
                sensorId = e.SensorId,
                eventType = e.EventType,
                soilMoisture = e.SoilMoisture,
                soilTemperature = e.SoilTemperature,
                ph = e.Ph,
                notes = e.Notes,
                readingTimeUtc = e.ReadingTimeUtc
            });

            if (rows.Count >= limit) break;
        }

        _log.LogInformation("GetTelemetry farm={Farm} returned {Count} rows", farmId, rows.Count);
        return new OkObjectResult(rows);
    }
}
