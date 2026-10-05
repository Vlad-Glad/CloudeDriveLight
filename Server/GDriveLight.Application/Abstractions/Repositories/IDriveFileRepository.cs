using GDriveLight.Domain.Models;

namespace GDriveLight.Application.Abstractions.Repositories;

public interface IDriveFileRepository
{
    Task<DriveFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<DriveFile>> GetByFolderIdAsync(Guid folderId, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<DriveFile>> GetRootFilesForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    
    Task AddAsync(DriveFile file, CancellationToken cancellationToken = default);
    
    void Update(DriveFile file);
    
    void Delete(DriveFile file);
}
