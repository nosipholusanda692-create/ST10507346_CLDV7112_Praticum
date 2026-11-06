using System.ComponentModel.DataAnnotations;
using Azure;
using Azure.Data.Tables;

namespace AgriStream.Web.Models;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Farmer = "Farmer";
    public const string Technician = "Technician";
}

// ---------- Azure SQL (user management) ----------
public class AppRole
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = "";

    public List<AppUser> Users { get; set; } = new();
}

public class AppUser
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Username { get; set; } = "";

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = "";

    [Required, MaxLength(500)]
    public string PasswordHash { get; set; } = "";

    public int RoleId { get; set; }
    public AppRole? Role { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

// ---------- Azure Table Storage (telemetry) ----------
// PartitionKey = FarmId
// RowKey       = reverse-ticks_sensor_guid  (so the newest rows sort first)
public class TelemetryEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string SensorId { get; set; } = "";
    public string EventType { get; set; } = "reading"; // reading | irrigation | alert
    public double SoilMoisture { get; set; }
    public double SoilTemperature { get; set; }
    public double Ph { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset ReadingTimeUtc { get; set; }
}
