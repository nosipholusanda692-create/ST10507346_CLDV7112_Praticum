using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions;

/// <summary>
/// FUNCTION 4 - Queue triggered.
/// Reads each message from "telemetry-queue" and stores it in Table Storage.
/// If a message fails 5 times (host.json maxDequeueCount) it moves to "telemetry-queue-poison"
/// so nothing is silently lost. The function scales out automatically as the queue grows.
/// Dry soil (moisture below 15%) also writes an "alert" row for irrigation follow-up.
/// </summary>
public class QueueTelemetryProcessor
{
    private const double DryThreshold = 15.0;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly TableStore _store;
    private readonly ILogger<QueueTelemetryProcessor> _log;

    public QueueTelemetryProcessor(TableStore store, ILogger<QueueTelemetryProcessor> log)
    {
        _store = store;
        _log = log;
    }

    [Function("ProcessTelemetryQueue")]
    public async Task Run(
        [QueueTrigger("telemetry-queue", Connection = "StorageConnection")] string message)
    {
        var msg = JsonSerializer.Deserialize<TelemetryMessage>(message, Json)
                  ?? throw new InvalidOperationException("Queue message was empty.");

        await _store.Telemetry.UpsertEntityAsync(TelemetryEntity.From(msg));

        if (msg.EventType == "reading" && msg.SoilMoisture < DryThreshold)
        {
            var alert = new TelemetryMessage
            {
                FarmId = msg.FarmId,
                SensorId = msg.SensorId,
                EventType = "alert",
                SoilMoisture = msg.SoilMoisture,
                SoilTemperature = msg.SoilTemperature,
                Ph = msg.Ph,
                Notes = $"Soil moisture {msg.SoilMoisture:0.0}% is below {DryThreshold}% - irrigation recommended",
                ReadingTimeUtc = msg.ReadingTimeUtc
            };
            await _store.Telemetry.UpsertEntityAsync(TelemetryEntity.From(alert, "_alert"));
            _log.LogWarning("Dry soil alert for {Farm}/{Sensor}: {Moisture}%", msg.FarmId, msg.SensorId, msg.SoilMoisture);
        }
    }
}
