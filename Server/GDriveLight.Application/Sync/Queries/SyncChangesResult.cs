namespace GDriveLight.Application.Sync.Queries;

public record SyncChangesResult(
    IReadOnlyList<ChangeItem> Changes,
    long NextCursor,
    bool HasMore);
