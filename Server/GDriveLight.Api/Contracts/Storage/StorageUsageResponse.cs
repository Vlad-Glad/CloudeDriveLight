namespace GDriveLight.Api.Contracts.Storage;

public record StorageUsageResponse(
    long QuotaBytes,
    long UsedBytes,
    long AvailableBytes,
    bool IsOverQuota);
