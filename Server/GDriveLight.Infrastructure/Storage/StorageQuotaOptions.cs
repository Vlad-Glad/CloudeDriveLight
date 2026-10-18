using GDriveLight.Application.Storage;

namespace GDriveLight.Infrastructure.Storage;

public sealed class StorageQuotaOptions
{
    public const string SectionName = "Storage";

    public long DefaultQuotaBytes { get; set; } = DefaultStorageQuotaPolicy.LimitBytes;
}
