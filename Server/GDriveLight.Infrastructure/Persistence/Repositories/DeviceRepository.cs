using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Domain.Models;
using GDriveLight.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Infrastructure.Persistence.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly AppDbContext _dbContext;

    public DeviceRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<IEnumerable<Device>> GetDevicesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Devices
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .ToListAsync(cancellationToken);
        return entities.Select(e => e.ToDomain());
    }

    public Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        _dbContext.Devices.Add(device.ToEntity());
        return Task.CompletedTask;
    }

    public void Update(Device device)
    {
        _dbContext.Devices.Update(device.ToEntity());
    }

    public void Delete(Device device)
    {
        _dbContext.Devices.Remove(device.ToEntity());
    }
}
