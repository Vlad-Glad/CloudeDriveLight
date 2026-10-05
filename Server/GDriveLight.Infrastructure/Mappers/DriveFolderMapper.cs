using GDriveLight.Domain.Models;
using GDriveLight.Infrastructure.Entities;

namespace GDriveLight.Infrastructure.Mappers;

internal static class DriveFolderMapper
{
    public static DriveFolderEntity ToEntity(this DriveFolder domain)
    {
        return new DriveFolderEntity
        {
            Id = domain.Id,
            Name = domain.Name,
            ParentFolderId = domain.ParentFolderId,
            OwnerId = domain.OwnerId
        };
    }

    public static DriveFolder ToDomain(this DriveFolderEntity entity)
    {
        var domain = new DriveFolder(entity.Name, entity.OwnerId, entity.ParentFolderId);
        domain.SetPropertyValue(nameof(DriveFolder.Id), entity.Id);
        return domain;
    }
}
