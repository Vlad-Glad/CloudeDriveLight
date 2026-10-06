using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Domain.Models;
using GDriveLight.Infrastructure.Entities;
using GDriveLight.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Infrastructure.Persistence.Repositories;

public class DriveFolderRepository : IDriveFolderRepository
{
    private readonly AppDbContext _dbContext;

    public DriveFolderRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DriveFolder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.DriveFolders
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        return entity?.ToDomain();
    }

    public async Task<IEnumerable<DriveFolder>> GetRootFoldersForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.DriveFolders
            .AsNoTracking()
            .Where(f => f.OwnerId == userId && f.ParentFolderId == null)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToDomain());
    }

    public async Task<IEnumerable<DriveFolder>> GetSubFoldersAsync(Guid parentFolderId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.DriveFolders
            .AsNoTracking()
            .Where(f => f.ParentFolderId == parentFolderId)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToDomain());
    }

    public async Task<bool> ExistsAsync(string name, Guid? parentFolderId, Guid ownerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DriveFolders
            .AnyAsync(f => f.Name == name && f.ParentFolderId == parentFolderId && f.OwnerId == ownerId, cancellationToken);
    }

    public async Task<IEnumerable<DriveFolder>> SearchByNameAsync(string name, Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.DriveFolders
            .AsNoTracking()
            .Where(f => f.OwnerId == userId && f.Name.Contains(name))
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToDomain());
    }

    public Task AddAsync(DriveFolder folder, CancellationToken cancellationToken = default)
    {
        _dbContext.DriveFolders.Add(folder.ToEntity());
        return Task.CompletedTask;
    }

    public void Update(DriveFolder folder)
    {
        _dbContext.DriveFolders.Update(folder.ToEntity());
    }

    public void Delete(DriveFolder folder)
    {
        _dbContext.DriveFolders.Remove(folder.ToEntity());
    }

    public async Task DeleteRecursivelyAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        var foldersToDelete = new List<DriveFolderEntity>();
        await CollectFoldersRecursively(folderId, foldersToDelete, cancellationToken);

        foldersToDelete.Reverse();
        _dbContext.DriveFolders.RemoveRange(foldersToDelete);
    }

    private async Task CollectFoldersRecursively(Guid folderId, List<DriveFolderEntity> result, CancellationToken cancellationToken)
    {
        var folder = await _dbContext.DriveFolders.FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken);
        if (folder == null) return;

        result.Add(folder);

        var subFolderIds = await _dbContext.DriveFolders
            .Where(f => f.ParentFolderId == folderId)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);

        foreach (var subId in subFolderIds)
        {
            await CollectFoldersRecursively(subId, result, cancellationToken);
        }
    }
}
