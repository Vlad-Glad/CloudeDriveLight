using GDriveLight.Application.Abstractions.Services;

namespace GDriveLight.Application.Storage;

public sealed class DefaultStorageQuotaPolicy : IStorageQuotaPolicy
{
    public const long LimitBytes = 2L * 1024 * 1024 * 1024;
    private readonly long _limitBytes;

    public DefaultStorageQuotaPolicy(long limitBytes = LimitBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limitBytes);
        _limitBytes = limitBytes;
    }

    public long GetLimitBytes(Guid userId) => _limitBytes;
}
