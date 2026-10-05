using GDriveLight.Domain.Models;
using GDriveLight.Infrastructure.Entities;

namespace GDriveLight.Infrastructure.Mappers;

internal static class SyncFileStateMapper
{
    public static SyncFileStateEntity ToEntity(this SyncFileState domain)
    {
        return new SyncFileStateEntity
        {
            Id = domain.Id,
            SyncFolderId = domain.SyncFolderId,
            DriveFileId = domain.DriveFileId,
            RelativePath = domain.RelativePath,
            LocalHash = domain.LocalHash,
            LastSyncedHash = domain.LastSyncedHash,
            LastSyncedAtUtc = domain.LastSyncedAtUtc,
            LocalModifiedAtUtc = domain.LocalModifiedAtUtc,
            Status = domain.Status
        };
    }

    public static SyncFileState ToDomain(this SyncFileStateEntity entity)
    {
        var domain = new SyncFileState(
            entity.SyncFolderId, 
            entity.RelativePath, 
            entity.LocalHash, 
            entity.LocalModifiedAtUtc, 
            entity.DriveFileId);
            
        domain.SetPropertyValue(nameof(SyncFileState.Id), entity.Id);
        domain.SetPropertyValue(nameof(SyncFileState.LastSyncedHash), entity.LastSyncedHash);
        domain.SetPropertyValue(nameof(SyncFileState.LastSyncedAtUtc), entity.LastSyncedAtUtc);
        domain.SetPropertyValue(nameof(SyncFileState.Status), entity.Status);
        
        return domain;
    }
}
