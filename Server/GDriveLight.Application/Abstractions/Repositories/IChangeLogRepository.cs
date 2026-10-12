using GDriveLight.Domain.Models;

namespace GDriveLight.Application.Abstractions.Repositories;

public interface IChangeLogRepository
{
    Task AddAsync(ChangeLogEntry entry, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<ChangeLogEntry> entries, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChangeLogEntry>> GetChangesAsync(Guid userId, long cursor, int limit = 100, CancellationToken cancellationToken = default);
}
