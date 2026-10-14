namespace GDriveLight.Desktop.Models.Api;

public record DeviceResponse(Guid Id, string Name, Guid? SyncRootFolderId, string? LocalPath);
