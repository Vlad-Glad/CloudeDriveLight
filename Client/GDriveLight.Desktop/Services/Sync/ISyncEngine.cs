namespace GDriveLight.Desktop.Services.Sync;

public record SyncResult(
    bool Success,
    int ProcessedChangesCount,
    string? Message = null,
    int FailedCount = 0,
    IReadOnlyList<string>? Errors = null);

public interface ISyncEngine
{
    Task<SyncResult> SynchronizeAsync(Guid userId, string localFolderPath, CancellationToken cancellationToken = default);
}
