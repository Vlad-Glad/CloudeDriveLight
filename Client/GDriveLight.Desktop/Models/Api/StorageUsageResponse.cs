using System.Text.Json.Serialization;

namespace GDriveLight.Desktop.Models.Api;

public record StorageUsageResponse(
    [property: JsonRequired] long QuotaBytes,
    [property: JsonRequired] long UsedBytes,
    [property: JsonRequired] long AvailableBytes,
    [property: JsonRequired] bool IsOverQuota);
