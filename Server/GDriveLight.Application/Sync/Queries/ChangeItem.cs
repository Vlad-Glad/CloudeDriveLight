using GDriveLight.Domain.Enums;

namespace GDriveLight.Application.Sync.Queries;

public record ChangeItem(
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
