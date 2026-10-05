using GDriveLight.Domain.Models;

namespace GDriveLight.Application.Abstractions.Repositories;

public interface ISyncFolderRepository
{
    Task<SyncFolder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<SyncFolder>> GetByDeviceIdAsync(Guid deviceId, CancellationToken cancellationToken = default);
    
    Task AddAsync(SyncFolder syncFolder, CancellationToken cancellationToken = default);
    
    void Update(SyncFolder syncFolder);
    
    void Delete(SyncFolder syncFolder);
}
