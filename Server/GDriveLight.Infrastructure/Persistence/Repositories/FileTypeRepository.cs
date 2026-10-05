using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Domain.Models;
using GDriveLight.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Infrastructure.Persistence.Repositories;

public class FileTypeRepository : IFileTypeRepository
{
    private readonly AppDbContext _dbContext;

    public FileTypeRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FileType?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.FileTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
            
        return entity?.ToDomain();
    }

    public async Task<FileType?> GetByExtensionAsync(string extension, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.FileTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Extension == extension.ToLowerInvariant(), cancellationToken);
            
        return entity?.ToDomain();
    }

    public async Task<IEnumerable<FileType>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.FileTypes
            .AsNoTracking()
            .ToListAsync(cancellationToken);
            
        return entities.Select(e => e.ToDomain());
    }
}
