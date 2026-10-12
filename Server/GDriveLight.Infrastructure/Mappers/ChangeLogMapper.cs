using GDriveLight.Domain.Models;
using GDriveLight.Infrastructure.Entities;

namespace GDriveLight.Infrastructure.Mappers;

internal static class ChangeLogMapper
{
    public static ChangeLogEntity ToEntity(this ChangeLogEntry domain)
    {
        return new ChangeLogEntity
        {
            Id = domain.Id,
            ResourceId = domain.ResourceId,
            ResourceType = domain.ResourceType,
            EventType = domain.EventType,
            UserId = domain.UserId,
            OriginDeviceId = domain.OriginDeviceId,
            ParentFolderId = domain.ParentFolderId,
            Name = domain.Name,
            ContentHash = domain.ContentHash,
            SizeBytes = domain.SizeBytes,
            TimestampUtc = domain.TimestampUtc
        };
    }

    public static ChangeLogEntry ToDomain(this ChangeLogEntity entity)
    {
        return new ChangeLogEntry(
            entity.ResourceId,
            entity.ResourceType,
            entity.EventType,
            entity.UserId,
            entity.OriginDeviceId,
            entity.ParentFolderId,
            entity.Name,
            entity.ContentHash,
            entity.SizeBytes,
            entity.Id,
            entity.TimestampUtc);
    }
}
