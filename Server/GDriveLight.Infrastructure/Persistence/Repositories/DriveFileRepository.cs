using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Domain.Models;
using GDriveLight.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Infrastructure.Persistence.Repositories;

public class DriveFileRepository : IDriveFileRepository
{
    private readonly AppDbContext _dbContext;

    public DriveFileRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DriveFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.DriveFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
            
        return entity?.ToDomain();
    }

    public async Task<IEnumerable<DriveFile>> GetByFolderIdAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.DriveFiles
            .AsNoTracking()
            .Where(f => f.FolderId == folderId)
            .ToListAsync(cancellationToken);
            
        return entities.Select(e => e.ToDomain());
    }

    public async Task<IEnumerable<DriveFile>> GetRootFilesForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.DriveFiles
            .AsNoTracking()
            .Where(f => f.OwnerId == userId && f.FolderId == null)
            .ToListAsync(cancellationToken);
            
        return entities.Select(e => e.ToDomain());
    }

    public Task AddAsync(DriveFile file, CancellationToken cancellationToken = default)
    {
        var entity = file.ToEntity();
        _dbContext.DriveFiles.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(DriveFile file)
    {
        var entity = file.ToEntity();
        _dbContext.DriveFiles.Update(entity);
    }

    public void Delete(DriveFile file)
    {
        var entity = file.ToEntity();
        _dbContext.DriveFiles.Remove(entity);
    }
}
