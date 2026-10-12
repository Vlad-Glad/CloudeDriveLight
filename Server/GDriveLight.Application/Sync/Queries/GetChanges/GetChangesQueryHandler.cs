using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Sync.Queries;

public class GetChangesQueryHandler : IRequestHandler<GetChangesQuery, Result<SyncChangesResult>>
{
    private readonly IChangeLogRepository _changeLogRepository;

    public GetChangesQueryHandler(IChangeLogRepository changeLogRepository)
    {
        _changeLogRepository = changeLogRepository;
    }

    public async Task<Result<SyncChangesResult>> Handle(GetChangesQuery request, CancellationToken cancellationToken)
    {
        var entries = await _changeLogRepository.GetChangesAsync(
            request.UserId,
            request.Cursor,
            request.Limit,
            cancellationToken);

        var items = entries.Select(e => new ChangeItem(
            e.Id,
            e.ResourceId,
            e.ResourceType,
            e.EventType,
            e.OriginDeviceId,
            e.ParentFolderId,
            e.Name,
            e.ContentHash,
            e.SizeBytes,
            e.TimestampUtc)).ToList();

        var nextCursor = items.Count > 0 ? items[^1].Id : request.Cursor;
        var hasMore = items.Count == request.Limit;

        return Result<SyncChangesResult>.Success(new SyncChangesResult(items, nextCursor, hasMore));
    }
}
