using GDriveLight.Desktop.Models.Sync;

namespace GDriveLight.Desktop.Models.Api;

public record ChangeResponse(
    long Id,
    Guid ResourceId,
    ResourceType ResourceType,
    ChangeType EventType,
    Guid? OriginDeviceId,
    Guid? ParentFolderId,
    string? Name,
    string? ContentHash,
    long? SizeBytes,
    DateTime TimestampUtc);
