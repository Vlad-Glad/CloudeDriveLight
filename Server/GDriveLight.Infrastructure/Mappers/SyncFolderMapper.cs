using GDriveLight.Domain.Models;
using GDriveLight.Infrastructure.Entities;

namespace GDriveLight.Infrastructure.Mappers;

internal static class SyncFolderMapper
{
    public static SyncFolderEntity ToEntity(this SyncFolder domain)
    {
        return new SyncFolderEntity
        {
            Id = domain.Id,
            LocalPath = domain.LocalPath,
            DriveFolderId = domain.DriveFolderId,
            DeviceId = domain.DeviceId,
            LastSyncedAtUtc = domain.LastSyncedAtUtc,
            SyncMode = domain.SyncMode
        };
    }

    public static SyncFolder ToDomain(this SyncFolderEntity entity)
    {
        var domain = new SyncFolder(entity.LocalPath, entity.DriveFolderId, entity.DeviceId, entity.SyncMode);
        domain.SetPropertyValue(nameof(SyncFolder.Id), entity.Id);
        domain.SetPropertyValue(nameof(SyncFolder.LastSyncedAtUtc), entity.LastSyncedAtUtc);
        return domain;
    }
}
