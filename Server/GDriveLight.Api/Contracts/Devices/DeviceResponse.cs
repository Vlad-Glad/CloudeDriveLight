namespace GDriveLight.Api.Contracts.Devices;

public record DeviceResponse(Guid Id, string Name, Guid? SyncRootFolderId, string? LocalPath);
