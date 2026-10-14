using GDriveLight.Application.Abstractions.Repositories;
using MediatR;

namespace GDriveLight.Application.Devices.Queries;

public record DeviceInfo(Guid Id, string Name, Guid? SyncRootFolderId, string? LocalPath);
public record GetDevicesQuery(Guid UserId) : IRequest<IEnumerable<DeviceInfo>>;

public sealed class GetDevicesQueryHandler(IDeviceRepository devices, ISyncFolderRepository syncFolders)
    : IRequestHandler<GetDevicesQuery, IEnumerable<DeviceInfo>>
{
    public async Task<IEnumerable<DeviceInfo>> Handle(GetDevicesQuery request, CancellationToken cancellationToken)
    {
        var result = new List<DeviceInfo>();
        foreach (var device in await devices.GetDevicesByUserIdAsync(request.UserId, cancellationToken))
        {
            var folder = (await syncFolders.GetByDeviceIdAsync(device.Id, cancellationToken)).FirstOrDefault();
            result.Add(new DeviceInfo(device.Id, device.Name, folder?.DriveFolderId, folder?.LocalPath));
        }
        return result.OrderBy(d => d.Name);
    }
}
