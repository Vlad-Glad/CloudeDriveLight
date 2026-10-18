namespace GDriveLight.Application.Common.Exceptions;

public sealed class FileSizeLimitExceededException(long maxFileSizeBytes)
    : Exception($"File size exceeds the maximum allowed size of {maxFileSizeBytes} bytes.")
{
    public long MaxFileSizeBytes { get; } = maxFileSizeBytes;
}
