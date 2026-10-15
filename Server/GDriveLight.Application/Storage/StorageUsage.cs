using GDriveLight.Application.Common.Exceptions;

namespace GDriveLight.Application.Storage;

public sealed record StorageUsage(long QuotaBytes, long UsedBytes)
{
    public long AvailableBytes => Math.Max(0, QuotaBytes - UsedBytes);
    public bool IsOverQuota => UsedBytes > QuotaBytes;

    // Call under the user's write transaction, using sizes read inside that transaction.
    public void EnsureCanGrow(long newSizeBytes, long oldSizeBytes = 0)
    {
        var increase = newSizeBytes - oldSizeBytes;
        if (increase > 0 && increase > AvailableBytes)
            throw new StorageQuotaExceededException(QuotaBytes, UsedBytes, increase);
    }
}
