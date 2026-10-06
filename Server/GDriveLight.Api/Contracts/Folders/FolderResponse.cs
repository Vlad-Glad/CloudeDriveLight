namespace GDriveLight.Api.Contracts.Folders;

public record FolderResponse(Guid Id, string Name, Guid? ParentFolderId, Guid OwnerId);
