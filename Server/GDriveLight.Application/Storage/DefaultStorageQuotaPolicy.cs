using GDriveLight.Application.Abstractions.Services;

namespace GDriveLight.Application.Storage;

public sealed class DefaultStorageQuotaPolicy : IStorageQuotaPolicy
{
    public const long LimitBytes = 2L * 1024 * 1024 * 1024;

    public long GetLimitBytes(Guid userId) => LimitBytes;
}
