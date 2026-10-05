using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Domain.Models;
using GDriveLight.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Infrastructure.Persistence.Repositories;

public class SyncFileStateRepository : ISyncFileStateRepository
{
    private readonly AppDbContext _dbContext;

    public SyncFileStateRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SyncFileState?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.SyncFileStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            
        return entity?.ToDomain();
    }

    public async Task<SyncFileState?> GetByFileAndDeviceAsync(Guid fileId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.SyncFileStates
            .AsNoTracking()
            .Include(s => s.SyncFolder)
            .FirstOrDefaultAsync(s => s.DriveFileId == fileId && s.SyncFolder!.DeviceId == deviceId, cancellationToken);
            
        return entity?.ToDomain();
    }

    public async Task<IEnumerable<SyncFileState>> GetByDeviceIdAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.SyncFileStates
            .AsNoTracking()
            .Include(s => s.SyncFolder)
            .Where(s => s.SyncFolder!.DeviceId == deviceId)
            .ToListAsync(cancellationToken);
            
        return entities.Select(e => e.ToDomain());
    }

    public Task AddAsync(SyncFileState syncFileState, CancellationToken cancellationToken = default)
    {
        _dbContext.SyncFileStates.Add(syncFileState.ToEntity());
        return Task.CompletedTask;
    }

    public void Update(SyncFileState syncFileState)
    {
        _dbContext.SyncFileStates.Update(syncFileState.ToEntity());
    }

    public void Delete(SyncFileState syncFileState)
    {
        _dbContext.SyncFileStates.Remove(syncFileState.ToEntity());
    }
}
