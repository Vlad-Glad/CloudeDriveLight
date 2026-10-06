namespace GDriveLight.Desktop.Models.Api;

public record FolderResponse(Guid Id, string Name, Guid? ParentFolderId, Guid OwnerId);
