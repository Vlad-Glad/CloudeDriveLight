using GDriveLight.Domain.Enums;

namespace GDriveLight.Domain.Models;

public class ChangeLogEntry
{
    private const int MaxNameLength = 255;
    private const int MaxContentHashLength = 128;

    public long Id { get; private set; }
    public Guid ResourceId { get; private set; }
    public ResourceType ResourceType { get; private set; }
    public ChangeType EventType { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? OriginDeviceId { get; private set; }
    public Guid? ParentFolderId { get; private set; }
    public string? Name { get; private set; }
    public string? ContentHash { get; private set; }
    public long? SizeBytes { get; private set; }
    public DateTime TimestampUtc { get; private set; }

    public ChangeLogEntry(
        Guid resourceId,
        ResourceType resourceType,
        ChangeType eventType,
        Guid userId,
        Guid? originDeviceId = null,
        Guid? parentFolderId = null,
        string? name = null,
        string? contentHash = null,
        long? sizeBytes = null,
        long id = 0,
        DateTime? timestampUtc = null)
    {
        if (resourceId == Guid.Empty)
            throw new ArgumentException("Resource ID cannot be an empty GUID.", nameof(resourceId));

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be an empty GUID.", nameof(userId));

        if (originDeviceId.HasValue && originDeviceId.Value == Guid.Empty)
            throw new ArgumentException("Origin Device ID cannot be an empty GUID.", nameof(originDeviceId));

        if (parentFolderId.HasValue && parentFolderId.Value == Guid.Empty)
            throw new ArgumentException("Parent Folder ID cannot be an empty GUID.", nameof(parentFolderId));

        if (name != null && name.Length > MaxNameLength)
            throw new ArgumentException($"Name cannot exceed {MaxNameLength} characters.", nameof(name));

        if (contentHash != null && contentHash.Length > MaxContentHashLength)
            throw new ArgumentException($"Content hash cannot exceed {MaxContentHashLength} characters.", nameof(contentHash));

        if (sizeBytes.HasValue && sizeBytes.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Size bytes cannot be negative.");

        Id = id;
        ResourceId = resourceId;
        ResourceType = resourceType;
        EventType = eventType;
        UserId = userId;
        OriginDeviceId = originDeviceId;
        ParentFolderId = parentFolderId;
        Name = name;
        ContentHash = contentHash;
        SizeBytes = sizeBytes;
        TimestampUtc = timestampUtc ?? DateTime.UtcNow;
    }

    public static ChangeLogEntry CreateFileChange(
        Guid fileId,
        ChangeType eventType,
        Guid userId,
        Guid? folderId,
        string? name,
        string? contentHash,
        long? sizeBytes,
        Guid? originDeviceId = null)
    {
        return new ChangeLogEntry(
            fileId,
            ResourceType.File,
            eventType,
            userId,
            originDeviceId,
            folderId,
            name,
            contentHash,
            sizeBytes);
    }

    public static ChangeLogEntry CreateFolderChange(
        Guid folderId,
        ChangeType eventType,
        Guid userId,
        Guid? parentFolderId,
        string? name,
        Guid? originDeviceId = null)
    {
        return new ChangeLogEntry(
            folderId,
            ResourceType.Folder,
            eventType,
            userId,
            originDeviceId,
            parentFolderId,
            name,
            contentHash: null,
            sizeBytes: null);
    }
}
