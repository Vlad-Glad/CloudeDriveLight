namespace GDriveLight.Api.Contracts.Sync;

public record SyncChangesResponse(
    IReadOnlyList<ChangeDto> Changes,
    long NextCursor,
    bool HasMore);
