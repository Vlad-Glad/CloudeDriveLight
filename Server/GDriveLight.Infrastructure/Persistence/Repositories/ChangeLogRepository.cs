using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Domain.Models;
using GDriveLight.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Infrastructure.Persistence.Repositories;

public class ChangeLogRepository : IChangeLogRepository
{
    private readonly AppDbContext _dbContext;

    public ChangeLogRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(ChangeLogEntry entry, CancellationToken cancellationToken = default)
    {
        _dbContext.ChangeLogs.Add(entry.ToEntity());
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<ChangeLogEntry> entries, CancellationToken cancellationToken = default)
    {
        _dbContext.ChangeLogs.AddRange(entries.Select(e => e.ToEntity()));
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ChangeLogEntry>> GetChangesAsync(Guid userId, long cursor, int limit = 100, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.ChangeLogs
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.Id > cursor)
            .OrderBy(c => c.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToDomain()).ToList();
    }
}
