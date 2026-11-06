using System.ComponentModel.DataAnnotations;

namespace AgriStream.Web.Models;

public class LoginVm
{
    [Required]
    public string Username { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public class RegisterVm
{
    [Required, StringLength(100, MinimumLength = 3)]
    public string Username { get; set; } = "";

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";
}

public class TelemetryPageVm
{
    public string FarmId { get; set; } = "farm-001";
    public List<TelemetryEntity> Readings { get; set; } = new();
}

public class MediaItem
{
    public string BlobName { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public long SizeBytes { get; set; }
    public string UploadedBy { get; set; } = "";
    public DateTimeOffset? UploadedOn { get; set; }
    public bool Processed { get; set; }
    public string Url { get; set; } = "";
    public bool IsImage { get; set; }
}

public class AdminVm
{
    public List<AppUser> Users { get; set; } = new();
    public List<AppRole> Roles { get; set; } = new();
}
