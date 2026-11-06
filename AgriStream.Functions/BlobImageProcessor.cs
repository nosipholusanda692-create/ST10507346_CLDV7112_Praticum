using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions;

/// <summary>
/// FUNCTION 1 - Blob triggered.
/// Fires whenever the web app (or a drone uploader) drops a file into the "farm-media" container.
/// It reads the blob properties and writes a row to the "MediaLog" table so the web app can show
/// the file as "Processed". (Real image analysis / thumbnails would be added here.)
///
/// NOTE: it deliberately does NOT modify the blob, because changing a blob would re-trigger it.
/// </summary>
public class BlobImageProcessor
{
    private readonly TableStore _store;
    private readonly ILogger<BlobImageProcessor> _log;

    public BlobImageProcessor(TableStore store, ILogger<BlobImageProcessor> log)
    {
        _store = store;
        _log = log;
    }

    [Function("ProcessDroneImage")]
    public async Task Run(
        [BlobTrigger("farm-media/{name}", Connection = "StorageConnection")] BlobClient blob,
        string name)
    {
        var props = (await blob.GetPropertiesAsync()).Value;
        var isImage = props.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

        var uploadedBy = props.Metadata.TryGetValue("uploadedBy", out var u)
            ? Uri.UnescapeDataString(u)
            : "unknown";

        var entity = new TableEntity("media", SafeKey(name))
        {
            { "BlobName", name },
            { "SizeBytes", props.ContentLength },
            { "ContentType", props.ContentType ?? "unknown" },
            { "UploadedBy", uploadedBy },
            { "Status", isImage ? "Processed" : "Stored (not an image)" },
            { "ProcessedUtc", DateTimeOffset.UtcNow }
        };

        await _store.MediaLog.UpsertEntityAsync(entity);
        _log.LogInformation("Processed blob {Name} ({Size} bytes, {Type}) uploaded by {User}",
            name, props.ContentLength, props.ContentType, uploadedBy);
    }

    // Table keys cannot contain / \ # ?
    private static string SafeKey(string s) =>
        s.Replace('/', '_').Replace('\\', '_').Replace('#', '_').Replace('?', '_');
}
