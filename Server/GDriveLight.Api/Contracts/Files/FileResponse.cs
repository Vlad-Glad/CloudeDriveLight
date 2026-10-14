namespace GDriveLight.Api.Contracts.Files;

public record FileResponse(
    Guid Id,
    string Name,
    int FileTypeId,
    string ContentHash,
    long SizeBytes,
    Guid? FolderId,
    Guid OwnerId,
    DateTime UploadedAtUtc,
    DateTime? EditedAtUtc,
    string FileUrl);
