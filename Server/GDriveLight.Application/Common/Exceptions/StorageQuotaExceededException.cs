namespace GDriveLight.Application.Common.Exceptions;

public sealed class StorageQuotaExceededException(long quotaBytes, long usedBytes, long requiredBytes)
    : Exception("There is not enough storage space. Delete files to free up space and try again.")
{
    public long QuotaBytes { get; } = quotaBytes;
    public long UsedBytes { get; } = usedBytes;
    public long RequiredBytes { get; } = requiredBytes;
}
