namespace GDriveLight.Desktop.Models.Api;

public record SyncChangesResponse(
    IReadOnlyList<ChangeResponse> Changes,
    long NextCursor,
    bool HasMore);
