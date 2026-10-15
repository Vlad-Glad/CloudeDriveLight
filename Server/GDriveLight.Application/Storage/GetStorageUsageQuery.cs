using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Abstractions.Services;
using MediatR;

namespace GDriveLight.Application.Storage;

public sealed record GetStorageUsageQuery(Guid UserId) : IRequest<StorageUsage>;

public sealed class GetStorageUsageQueryHandler(IDriveFileRepository files, IStorageQuotaPolicy policy)
    : IRequestHandler<GetStorageUsageQuery, StorageUsage>
{
    public async Task<StorageUsage> Handle(GetStorageUsageQuery request, CancellationToken cancellationToken) =>
        new(policy.GetLimitBytes(request.UserId),
            await files.GetUsedBytesForUserAsync(request.UserId, cancellationToken));
}
