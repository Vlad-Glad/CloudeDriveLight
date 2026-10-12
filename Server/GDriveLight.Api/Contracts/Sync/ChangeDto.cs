using GDriveLight.Domain.Enums;

namespace GDriveLight.Api.Contracts.Sync;

public record ChangeDto(
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
