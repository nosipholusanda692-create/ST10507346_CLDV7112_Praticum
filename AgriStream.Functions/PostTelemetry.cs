using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions;

/// <summary>Return type: one queue message per reading + the HTTP response.</summary>
public class PostTelemetryOutput
{
    [QueueOutput("telemetry-queue", Connection = "StorageConnection")]
    public string[]? QueueMessages { get; set; }

    [HttpResult]
    public IActionResult HttpResponse { get; set; } = null!;
}

/// <summary>
/// FUNCTION 3 - HTTP triggered (POST).
///   POST /api/telemetry   body: one JSON reading OR an array of readings
/// Validates the payload and drops each reading on the "telemetry-queue" Storage Queue, then
/// immediately answers 202 Accepted. The queue absorbs the surge so no data is lost when
/// thousands of sensors report at the same moment.
/// </summary>
public class PostTelemetry
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ILogger<PostTelemetry> _log;

    public PostTelemetry(ILogger<PostTelemetry> log) => _log = log;

    [Function("PostTelemetry")]
    public async Task<PostTelemetryOutput> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "telemetry")] HttpRequest req)
    {
        List<TelemetryMessage> readings;
        try
        {
            using var doc = await JsonDocument.ParseAsync(req.Body);
            readings = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.Deserialize<List<TelemetryMessage>>(Json) ?? new()
                : new() { doc.RootElement.Deserialize<TelemetryMessage>(Json)! };
        }
        catch (Exception ex) when (ex is JsonException or ArgumentNullException or NullReferenceException)
        {
            return Fail("Body must be valid JSON: one reading or an array of readings.");
        }

        if (readings.Count == 0) return Fail("No readings supplied.");
        if (readings.Count > 500) return Fail("Maximum 500 readings per request.");

        foreach (var r in readings)
        {
            if (string.IsNullOrWhiteSpace(r.FarmId) || string.IsNullOrWhiteSpace(r.SensorId))
                return Fail("farmId and sensorId are required on every reading.");
            if (r.SoilMoisture is < 0 or > 100)
                return Fail("soilMoisture must be between 0 and 100.");
            if (r.Ph is < 0 or > 14)
                return Fail("ph must be between 0 and 14.");

            r.ReadingTimeUtc ??= DateTimeOffset.UtcNow;
        }

        _log.LogInformation("Queued {Count} telemetry readings", readings.Count);

        return new PostTelemetryOutput
        {
            QueueMessages = readings.Select(r => JsonSerializer.Serialize(r, Json)).ToArray(),
            HttpResponse = new ObjectResult(new { queued = readings.Count }) { StatusCode = StatusCodes.Status202Accepted }
        };
    }

    private static PostTelemetryOutput Fail(string message) => new()
    {
        HttpResponse = new BadRequestObjectResult(new { error = message })
    };
}
