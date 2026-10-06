namespace GDriveLight.Application.Files.Queries;

public record FileQuery(
    Guid Id,
    string Name,
    int FileTypeId,
    string ContentHash,
    Guid? FolderId,
    Guid OwnerId,
    DateTime UploadedAtUtc,
    DateTime? EditedAtUtc,
    string FileUrl);
