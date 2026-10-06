namespace GDriveLight.Application.Folders.Queries;

public record FolderQuery(Guid Id, string Name, Guid? ParentFolderId, Guid OwnerId);
