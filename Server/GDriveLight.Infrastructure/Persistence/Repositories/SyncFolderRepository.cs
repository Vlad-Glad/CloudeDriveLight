using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Domain.Models;
using GDriveLight.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Infrastructure.Persistence.Repositories;

public class SyncFolderRepository : ISyncFolderRepository
{
    private readonly AppDbContext _dbContext;

    public SyncFolderRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SyncFolder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.SyncFolders
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            
        return entity?.ToDomain();
    }

    public async Task<IEnumerable<SyncFolder>> GetByDeviceIdAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.SyncFolders
            .AsNoTracking()
            .Where(s => s.DeviceId == deviceId)
            .ToListAsync(cancellationToken);
            
        return entities.Select(e => e.ToDomain());
    }

    public Task AddAsync(SyncFolder syncFolder, CancellationToken cancellationToken = default)
    {
        _dbContext.SyncFolders.Add(syncFolder.ToEntity());
        return Task.CompletedTask;
    }

    public void Update(SyncFolder syncFolder)
    {
        _dbContext.SyncFolders.Update(syncFolder.ToEntity());
    }

    public void Delete(SyncFolder syncFolder)
    {
        _dbContext.SyncFolders.Remove(syncFolder.ToEntity());
    }
}
