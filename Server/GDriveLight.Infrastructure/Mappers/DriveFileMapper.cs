using GDriveLight.Domain.Models;
using GDriveLight.Infrastructure.Entities;

namespace GDriveLight.Infrastructure.Mappers;

internal static class DriveFileMapper
{
    public static DriveFileEntity ToEntity(this DriveFile domain)
    {
        return new DriveFileEntity
        {
            Id = domain.Id,
            Name = domain.Name,
            FileUrl = domain.FileUrl,
            FileTypeId = domain.FileTypeId,
            ContentHash = domain.ContentHash,
            FolderId = domain.FolderId,
            OwnerId = domain.OwnerId,
            UploadedAtUtc = domain.UploadedAtUtc,
            EditedByUserId = domain.EditedByUserId,
            EditedAtUtc = domain.EditedAtUtc
        };
    }

    public static DriveFile ToDomain(this DriveFileEntity entity)
    {
        var domain = new DriveFile(
            entity.Name, 
            entity.FileUrl, 
            entity.FileTypeId, 
            entity.ContentHash, 
            entity.OwnerId, 
            entity.FolderId);
            
        domain.SetPropertyValue(nameof(DriveFile.Id), entity.Id);
        domain.SetPropertyValue(nameof(DriveFile.UploadedAtUtc), entity.UploadedAtUtc);
        domain.SetPropertyValue(nameof(DriveFile.EditedByUserId), entity.EditedByUserId);
        domain.SetPropertyValue(nameof(DriveFile.EditedAtUtc), entity.EditedAtUtc);
        
        return domain;
    }
}
