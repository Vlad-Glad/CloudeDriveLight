using GDriveLight.Domain.Models;

namespace GDriveLight.Application.Abstractions.Repositories;

public interface ISyncFileStateRepository
{
    Task<SyncFileState?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<SyncFileState?> GetByFileAndDeviceAsync(Guid fileId, Guid deviceId, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<SyncFileState>> GetByDeviceIdAsync(Guid deviceId, CancellationToken cancellationToken = default);
    
    Task AddAsync(SyncFileState syncFileState, CancellationToken cancellationToken = default);
    
    void Update(SyncFileState syncFileState);
    
    void Delete(SyncFileState syncFileState);
}
