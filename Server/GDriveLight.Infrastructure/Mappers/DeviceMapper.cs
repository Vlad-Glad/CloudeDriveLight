using GDriveLight.Domain.Models;
using GDriveLight.Infrastructure.Entities;

namespace GDriveLight.Infrastructure.Mappers;

internal static class DeviceMapper
{
    public static DeviceEntity ToEntity(this Device domain)
    {
        return new DeviceEntity
        {
            Id = domain.Id,
            UserId = domain.UserId,
            Name = domain.Name
        };
    }

    public static Device ToDomain(this DeviceEntity entity)
    {
        var domain = new Device(entity.UserId, entity.Name);
        domain.SetPropertyValue(nameof(Device.Id), entity.Id);
        
        return domain;
    }
}
