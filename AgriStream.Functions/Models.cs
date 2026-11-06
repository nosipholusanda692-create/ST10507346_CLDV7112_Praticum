using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;

namespace AgriStream.Functions;

/// <summary>JSON body accepted by the POST function and carried on the queue.</summary>
public class TelemetryMessage
{
    public string FarmId { get; set; } = "";
    public string SensorId { get; set; } = "";
    public string EventType { get; set; } = "reading"; // reading | irrigation | alert
    public double SoilMoisture { get; set; }
    public double SoilTemperature { get; set; }
    public double Ph { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? ReadingTimeUtc { get; set; }
}

/// <summary>Row stored in the "Telemetry" table. PartitionKey = FarmId, RowKey sorts newest first.</summary>
public class TelemetryEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string SensorId { get; set; } = "";
    public string EventType { get; set; } = "reading";
    public double SoilMoisture { get; set; }
    public double SoilTemperature { get; set; }
    public double Ph { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset ReadingTimeUtc { get; set; }

    public static TelemetryEntity From(TelemetryMessage m, string? suffix = null)
    {
        var time = m.ReadingTimeUtc ?? DateTimeOffset.UtcNow;
        var reverseTicks = (DateTime.MaxValue.Ticks - time.UtcTicks).ToString("D19");
        var unique = Guid.NewGuid().ToString("N")[..8];

        return new TelemetryEntity
        {
            PartitionKey = m.FarmId,
            RowKey = $"{reverseTicks}_{m.SensorId}_{unique}{suffix}",
            SensorId = m.SensorId,
            EventType = m.EventType,
            SoilMoisture = m.SoilMoisture,
            SoilTemperature = m.SoilTemperature,
            Ph = m.Ph,
            Notes = m.Notes,
            ReadingTimeUtc = time
        };
    }
}

/// <summary>Holds the TableClients so they are created once per host instance.</summary>
public class TableStore
{
    public TableClient Telemetry { get; }
    public TableClient MediaLog { get; }

    public TableStore(IConfiguration cfg)
    {
        var cs = cfg["StorageConnection"]
                 ?? throw new InvalidOperationException("App setting 'StorageConnection' is missing.");

        Telemetry = new TableClient(cs, "Telemetry");
        Telemetry.CreateIfNotExists();

        MediaLog = new TableClient(cs, "MediaLog");
        MediaLog.CreateIfNotExists();
    }
}
