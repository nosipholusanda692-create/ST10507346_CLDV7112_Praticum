using AgriStream.Web.Models;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace AgriStream.Web.Services;

/// <summary>
/// Wraps Azure Blob Storage (drone images / media) and Azure Table Storage (telemetry, media log).
/// </summary>
public class StorageService
{
    private const string ContainerName = "farm-media";
    private readonly BlobContainerClient _container;

    public TableClient Telemetry { get; }
    public TableClient MediaLog { get; }

    public StorageService(IConfiguration cfg)
    {
        var cs = cfg["Storage:ConnectionString"];
        if (string.IsNullOrWhiteSpace(cs) || cs.Contains('<'))
            throw new InvalidOperationException("Storage:ConnectionString is not configured.");

        _container = new BlobContainerClient(cs, ContainerName);
        _container.CreateIfNotExists(PublicAccessType.None);

        Telemetry = new TableClient(cs, "Telemetry");
        Telemetry.CreateIfNotExists();

        MediaLog = new TableClient(cs, "MediaLog");
        MediaLog.CreateIfNotExists();
    }

    // ---------------- Blob ----------------

    public async Task<string> UploadMediaAsync(IFormFile file, string uploadedBy)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var blobName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}{ext}";
        var blob = _container.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType },
            Metadata = new Dictionary<string, string>
            {
                ["uploadedBy"] = Uri.EscapeDataString(uploadedBy),
                ["originalName"] = Uri.EscapeDataString(file.FileName)
            }
        });

        return blobName;
    }

    public async Task<List<MediaItem>> ListMediaAsync(int take = 100)
    {
        // Which blobs were already handled by the blob-triggered function?
        var processed = new HashSet<string>();
        await foreach (var row in MediaLog.QueryAsync<TableEntity>(e => e.PartitionKey == "media", select: new[] { "RowKey" }))
            processed.Add(row.RowKey);

        var items = new List<MediaItem>();
        await foreach (var b in _container.GetBlobsAsync(BlobTraits.Metadata))
        {
            b.Metadata.TryGetValue("uploadedBy", out var by);
            b.Metadata.TryGetValue("originalName", out var original);
            var contentType = b.Properties.ContentType ?? "";

            items.Add(new MediaItem
            {
                BlobName = b.Name,
                OriginalName = original != null ? Uri.UnescapeDataString(original) : b.Name,
                UploadedBy = by != null ? Uri.UnescapeDataString(by) : "unknown",
                SizeBytes = b.Properties.ContentLength ?? 0,
                UploadedOn = b.Properties.CreatedOn,
                Processed = processed.Contains(b.Name),
                IsImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase),
                Url = _container.GetBlobClient(b.Name)
                        .GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddMinutes(15))
                        .ToString()
            });
        }

        return items.OrderByDescending(i => i.BlobName).Take(take).ToList();
    }

    // ---------------- Table ----------------

    public async Task<List<TelemetryEntity>> GetLatestTelemetryAsync(string farmId, int take = 50)
    {
        var list = new List<TelemetryEntity>();
        await foreach (var e in Telemetry.QueryAsync<TelemetryEntity>(x => x.PartitionKey == farmId, maxPerPage: take))
        {
            list.Add(e);
            if (list.Count >= take) break;
        }
        return list;
    }
}
