namespace GDriveLight.Api.Contracts.Files;

public record FileResponse(
    Guid Id,
    string Name,
    int FileTypeId,
    string ContentHash,
    Guid? FolderId,
    Guid OwnerId,
    DateTime UploadedAtUtc,
    DateTime? EditedAtUtc,
    string FileUrl);
