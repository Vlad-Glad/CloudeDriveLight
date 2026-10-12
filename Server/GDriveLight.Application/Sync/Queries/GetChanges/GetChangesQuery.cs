using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Sync.Queries;

public record GetChangesQuery(
    Guid UserId,
    long Cursor = 0,
    int Limit = 100) : IRequest<Result<SyncChangesResult>>;
