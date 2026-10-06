using GDriveLight.Domain.Models;

namespace GDriveLight.Application.Abstractions.Repositories;

public interface IDriveFolderRepository
{
    Task<DriveFolder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<DriveFolder>> GetRootFoldersForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IEnumerable<DriveFolder>> GetSubFoldersAsync(Guid parentFolderId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string name, Guid? parentFolderId, Guid ownerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<DriveFolder>> SearchByNameAsync(string name, Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(DriveFolder folder, CancellationToken cancellationToken = default);

    void Update(DriveFolder folder);

    void Delete(DriveFolder folder);

    Task DeleteRecursivelyAsync(Guid folderId, CancellationToken cancellationToken = default);
}