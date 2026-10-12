using GDriveLight.Domain.Enums;

namespace GDriveLight.Infrastructure.Entities;

public class ChangeLogEntity
{
    public long Id { get; set; }
    public Guid ResourceId { get; set; }
    public ResourceType ResourceType { get; set; }
    public ChangeType EventType { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUserEntity User { get; set; } = null!;
    public Guid? OriginDeviceId { get; set; }
    public DeviceEntity? OriginDevice { get; set; }
    public Guid? ParentFolderId { get; set; }
    public string? Name { get; set; }
    public string? ContentHash { get; set; }
    public long? SizeBytes { get; set; }
    public DateTime TimestampUtc { get; set; }
}
