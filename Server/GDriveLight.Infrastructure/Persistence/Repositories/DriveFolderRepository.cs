using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Domain.Models;
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
}
