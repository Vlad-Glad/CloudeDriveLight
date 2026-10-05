using GDriveLight.Domain.Models;
using GDriveLight.Infrastructure.Entities;

namespace GDriveLight.Infrastructure.Mappers;

internal static class FileTypeMapper
{
    public static FileTypeEntity ToEntity(this FileType domain)
    {
        return new FileTypeEntity
        {
            Id = domain.Id,
            Name = domain.Name,
            MimeType = domain.MimeType,
            Extension = domain.Extension
        };
    }

    public static FileType ToDomain(this FileTypeEntity entity)
    {
        var domain = new FileType(entity.Name, entity.MimeType, entity.Extension);
        domain.SetPropertyValue(nameof(FileType.Id), entity.Id);
        return domain;
    }
}
